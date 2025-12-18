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

        var vpd = CalculateVpd(sensorData);

        logger.LogInformation("Temperature: {Temperature:F2}°C, humidity: {Humidity:F2}%, VPD: {Vpd:F2} kPa", 
            sensorData.Temperature, sensorData.Humidity, vpd);

        telemetryManager.CurrentTemperature = sensorData.Temperature;
        telemetryManager.CurrentHumidity = sensorData.Humidity;
        telemetryManager.CurrentVpd = vpd;
    }

    /// <summary>
    ///     Calculates the VPD (Vapor Pressure Deficit) based on temperature and humidity.
    /// </summary>
    private static double CalculateVpd(SensorReading sensorData)
    {
        // SVP (Saturation Vapor Pressure) = 0.6108 * exp((17.27 * T) / (T + 237.3)) in kPa
        // VPD = SVP * (1 - RH/100)
        var svp = 0.6108 * Math.Exp((17.27 * sensorData.Temperature) / (sensorData.Temperature + 237.3));
        return svp * (1 - sensorData.Humidity / 100.0);
    }
}