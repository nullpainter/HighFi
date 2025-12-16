using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace HighFi.Builders;

public static class TelemetryBuilder
{
    private const string OtelCollectorUrl = "http://localhost:4317/v1/metrics";

    public static IServiceCollection AddTelemetry(this IServiceCollection services)
    {
        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("AcInfinityBridge",
                    serviceNamespace: "ac-infinity",
                    serviceInstanceId: "ac-infinity",
                    serviceVersion: "1.0.0")
            )
            .WithMetrics(metrics => metrics
                .AddMeter("AcInfinityBridge")
                .AddOtlpExporter((options, readerOptions) =>
                {
                    options.Endpoint = new Uri(OtelCollectorUrl);
                    options.Protocol = OtlpExportProtocol.Grpc;
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = (int)TimeSpan.FromSeconds(10).TotalMilliseconds;
                })
            );

        return services;
    }
}