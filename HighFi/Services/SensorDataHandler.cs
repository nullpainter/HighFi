using Microsoft.Extensions.Logging;

namespace HighFi.Services;

public class SensorDataHandler(
    TelemetryManager telemetryManager,
    DeviceDataParser dataParser,
    ILogger<SensorDataHandler> logger)
{
    public void HandleSensorData(byte[] data)
    {
        if (dataParser.ParseSensorData(data) is not { } sensorData) return;
        
        logger.LogInformation("Temperature: {Temperature:F2}°C, humidity: {Humidity:F2}%", sensorData.Temperature, sensorData.Humidity);

        telemetryManager.RecordTemperature(sensorData.Temperature);
        telemetryManager.RecordHumidity(sensorData.Humidity);
    }
}

