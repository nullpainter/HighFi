using HighFi.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HighFi.Builders;

public static class ApplicationBuilder
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services
            .AddSingleton<TelemetryManager>()
            .AddSingleton<DeviceDataParser>()
            .AddSingleton<SensorDataHandler>()
            .AddHostedService<BluetoothManager>();

        return services;
    }
}

