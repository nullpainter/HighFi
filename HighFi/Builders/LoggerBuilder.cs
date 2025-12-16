using Serilog;

namespace HighFi.Builders;

public static class LoggerBuilder
{
    public static void ConfigureSerilog()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();
    }
}

