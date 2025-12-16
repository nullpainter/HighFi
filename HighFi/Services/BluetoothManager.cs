using System.Diagnostics;
using InTheHand.Bluetooth;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HighFi.Services;

public class BluetoothManager(
    SensorDataHandler sensorDataHandler,
    TelemetryManager telemetryManager,
    ILogger<BluetoothManager> logger) : BackgroundService
{
    // AC Infinity device name
    private const string DeviceName = "ACI-E";
    
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromSeconds(5);
    
    private BluetoothDevice? _device;
    private GattCharacteristic? _notifyCharacteristic;
    private short _lastRssi;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndMonitorAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Bluetooth connection cycle");
            }

            await WaitBeforeReconnectAsync(stoppingToken);
        }
    }

    private async Task WaitBeforeReconnectAsync(CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested) return;
        
        logger.LogInformation("Waiting {Delay} before reconnection attempt", ReconnectDelay);
        await Task.Delay(ReconnectDelay, stoppingToken);
    }

    private async Task ConnectAndMonitorAsync(CancellationToken stoppingToken)
    {
        _device = await DiscoverAcInfinityDeviceAsync(stoppingToken);
        if (_device is null)
        {
            logger.LogWarning("AC Infinity device not found, will retry");
            return;
        }

        logger.LogInformation("Found AC Infinity device: {DeviceName} ({Id})", _device.Name, _device.Id);
        telemetryManager.CurrentRssi = _lastRssi;

        try
        {
            await EstablishConnectionAsync(_device.Gatt, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during device connection or monitoring");
        }
        finally
        {
            await DisconnectAsync();
        }
    }

    private async Task EstablishConnectionAsync(RemoteGattServer gatt, CancellationToken stoppingToken)
    {
        logger.LogInformation("Connected to device successfully");

        _notifyCharacteristic = await FindFirstNotifyCharacteristicAsync(gatt, stoppingToken);
        if (_notifyCharacteristic is null)
        {
            logger.LogWarning("No notify characteristic found on device");
            return;
        }

        logger.LogTrace("Found notify characteristic: {Uuid}", _notifyCharacteristic.Uuid);

        _notifyCharacteristic.CharacteristicValueChanged += OnCharacteristicValueChanged;
        await _notifyCharacteristic.StartNotificationsAsync();
        logger.LogTrace("Subscribed to notifications");

        await MonitorConnectionAsync(stoppingToken);
    }

    private async Task MonitorConnectionAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && _device?.Gatt.IsConnected == true)
        {
            await Task.Delay(1000, stoppingToken);
        }

        if (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning("Device disconnected, will attempt to reconnect");
        }
    }

    private async Task<BluetoothDevice?> DiscoverAcInfinityDeviceAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scanning for AC Infinity device");

        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Create a cancellation token that times out
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(ScanTimeout);
            
            BluetoothDevice? foundDevice = null;
            var deviceFoundSignal = new TaskCompletionSource<bool>();
            
            void OnAdvertisementReceived(object? sender, BluetoothAdvertisingEvent args)
            {
                if (foundDevice is not null) return; 
                if (args.Name != DeviceName) return;
                
                _lastRssi = args.Rssi;
                logger.LogDebug("Found matching device: {Name} ({Address}), RSSI: {Rssi} dBm", 
                    DeviceName, args.Device.Id, _lastRssi);
                
                foundDevice = args.Device;
                deviceFoundSignal.TrySetResult(true);
            }
            
            // Use event-based scanning for cross-platform compatibility
            Bluetooth.AdvertisementReceived += OnAdvertisementReceived;
            
            try
            {
                await Bluetooth.RequestLEScanAsync(new BluetoothLEScanOptions
                {
                    AcceptAllAdvertisements = true
                });
                
                // Wait for device found or timeout
                using (cts.Token.Register(() => deviceFoundSignal.TrySetResult(false)))
                {
                    await deviceFoundSignal.Task;
                }
            }
            finally
            {
                Bluetooth.AdvertisementReceived -= OnAdvertisementReceived;
            }
            
            logger.LogDebug("Scan completed in {Elapsed}", stopwatch.Elapsed);
            return foundDevice;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during device discovery");
        }

        return null;
    }

    private async Task<GattCharacteristic?> FindFirstNotifyCharacteristicAsync(
        RemoteGattServer gatt, 
        CancellationToken stoppingToken)
    {
        try
        {
            var services = await gatt.GetPrimaryServicesAsync();

            foreach (var service in services)
            {
                if (stoppingToken.IsCancellationRequested) return null;

                var characteristics = await service.GetCharacteristicsAsync();
                var notifyCharacteristic = characteristics.FirstOrDefault(c => 
                    c.Properties.HasFlag(GattCharacteristicProperties.Notify));
                
                if (notifyCharacteristic is not null)
                    return notifyCharacteristic;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding notify characteristic");
        }

        return null;
    }

    private void OnCharacteristicValueChanged(object? sender, GattCharacteristicValueChangedEventArgs e)
    {
        var data = e.Value;
        if (data is not { Length: > 0 }) return;
        
        var hexData = BitConverter.ToString(data).Replace("-", " ");
        var textData = System.Text.Encoding.UTF8.GetString(data);
        logger.LogTrace("Notification received: {Data} (Hex: {Hex})", textData, hexData);

        sensorDataHandler.HandleSensorData(data);
    }

    private async Task DisconnectAsync()
    {
        try
        {
            if (_notifyCharacteristic != null)
            {
                _notifyCharacteristic.CharacteristicValueChanged -= OnCharacteristicValueChanged;
                
                try
                {
                    await _notifyCharacteristic.StopNotificationsAsync();
                    logger.LogInformation("Unsubscribed from notifications");
                }
                catch (NotSupportedException)
                {
                    logger.LogDebug("StopNotifications not supported on this platform, skipping");
                }
                
                _notifyCharacteristic = null;
            }

            if (_device != null)
            {
                _device.Gatt.Disconnect();
                logger.LogInformation("Disconnected from device");
                _device = null;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during disconnect");
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Bluetooth Service stop requested");
        await DisconnectAsync();
        await base.StopAsync(stoppingToken);
    }
}