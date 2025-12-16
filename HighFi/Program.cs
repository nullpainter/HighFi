using HighFi.Builders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

LoggerBuilder.ConfigureSerilog();

try
{
    Log.Information("Starting HighFi");

    var builder = Host.CreateApplicationBuilder(args);
    
    builder.Services
        .AddApplicationServices()
        .AddTelemetry();

    builder.Logging
        .ClearProviders()
        .AddSerilog();

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "HighFi terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}