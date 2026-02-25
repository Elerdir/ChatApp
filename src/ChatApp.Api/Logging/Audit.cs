using Serilog;

namespace ChatApp.Api.Logging;

public static class Audit
{
    // Pozor: nikdy sem neposílej hesla nebo refresh tokeny.
    public static void Info(string action, object? data = null)
    {
        if (data is null)
            Log.ForContext("audit", true)
                .Information("AUDIT {Action}", action);
        else
            Log.ForContext("audit", true)
                .ForContext("data", data, destructureObjects: true)
                .Information("AUDIT {Action}", action);
    }

    public static void Warn(string action, object? data = null)
    {
        if (data is null)
            Log.ForContext("audit", true)
                .Warning("AUDIT {Action}", action);
        else
            Log.ForContext("audit", true)
                .ForContext("data", data, destructureObjects: true)
                .Warning("AUDIT {Action}", action);
    }
}