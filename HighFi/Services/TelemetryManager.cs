using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace HighFi.Services;

public class TelemetryManager : IDisposable
{
    private readonly Meter _meter;

    public double? CurrentTemperature { get; set; }
    public double? CurrentHumidity { get; set; }
    public short? CurrentRssi { get; set; }

    public TelemetryManager(ILogger<TelemetryManager> logger)
    {
        _meter = new Meter("AcInfinityBridge", "1.0.0");

        // Use ObservableGauge to report the current temperature value
        _meter.CreateObservableGauge(
            name: "ac_infinity_temperature",
            observeValue: () =>
            {
                if (CurrentTemperature.HasValue)
                {
                    logger.LogTrace("ObservableGauge callback: Reporting temperature {Temperature}°C", CurrentTemperature.Value);
                    return CurrentTemperature.Value;
                }

                logger.LogTrace("ObservableGauge callback: Temperature not yet available, skipping");
                return double.NaN; // Return NaN to indicate no value
            },
            description: "Current temperature reading from AC Infinity device");

        // Use ObservableGauge to report the current humidity value
        _meter.CreateObservableGauge(
            name: "ac_infinity_humidity",
            observeValue: () =>
            {
                if (CurrentHumidity.HasValue)
                {
                    logger.LogTrace("Reporting humidity {Humidity}%", CurrentHumidity.Value);
                    return CurrentHumidity.Value;
                }

                logger.LogTrace("Humidity not yet available, skipping");
                return double.NaN; // Return NaN to indicate no value
            },
            description: "Current humidity reading from AC Infinity device");

        // Use ObservableGauge to report the current RSSI (signal strength) value
        _meter.CreateObservableGauge(
            name: "ac_infinity_rssi",
            observeValue: () =>
            {
                if (CurrentRssi.HasValue)
                {
                    logger.LogTrace("Reporting RSSI {Rssi} dBm", CurrentRssi.Value);
                    return CurrentRssi.Value;
                }

                logger.LogTrace("RSSI not yet available, skipping");
                return double.NaN; // Return NaN to indicate no value
            },
            unit: "dBm",
            description: "Current Bluetooth signal strength (RSSI) from AC Infinity device");
    }

    public void RecordTemperature(double temperature) => CurrentTemperature = temperature;
    public void RecordHumidity(double humidity) => CurrentHumidity = humidity;
    public void RecordRssi(short rssi) => CurrentRssi = rssi;

    public void Dispose() => _meter.Dispose();
}