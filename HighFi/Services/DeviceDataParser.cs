using Microsoft.Extensions.Logging;

namespace HighFi.Services;

public readonly record struct SensorReading(double Temperature, double Humidity);

public class DeviceDataParser(ILogger<DeviceDataParser> logger)
{
    public SensorReading? ParseSensorData(byte[] data)
    {
        if (data.Length < 12)
        {
            logger.LogTrace("Data packet too short to parse sensor data (length: {Length})", data.Length);
            return null;
        }

        // Temperature at bytes 8-9 (big-endian, in 0.01°C units)
        var tempRaw = (data[8] << 8) | data[9];
        var temperature = tempRaw / 100.0;

        // Humidity at bytes 10-11 (big-endian, in 0.01% units)
        var humidityRaw = (data[10] << 8) | data[11];
        var humidity = humidityRaw / 100.0;

        logger.LogTrace("Parsed temperature: {Temperature}°C, humidity: {Humidity}%", temperature, humidity);

        return new SensorReading(temperature, humidity);
    }
}

