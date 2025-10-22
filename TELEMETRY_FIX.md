# Aspire OpenTelemetry OTLP Exporter Fix

## Problem
Aspire Dashboard was only showing **Console logs** - no structured logging, traces, or metrics were appearing despite OpenTelemetry being configured.

## Root Cause
The OpenTelemetry configuration had **two critical issues**:

1. **Missing Logging Exporter**: `builder.Logging.AddOpenTelemetry()` was configured but had **no exporter** - logging telemetry had nowhere to go.

2. **Wrong Exporter Pattern**: Cannot mix signal-specific exporters (`logging.AddOtlpExporter()`) with the cross-cutting exporter (`otel.UseOtlpExporter()`). OpenTelemetry throws `System.NotSupportedException` if both are used.

## Solution
Use **signal-specific OTLP exporters** for all three telemetry signals (logging, metrics, tracing).

### Updated Code (OrleansVoting.ServiceDefaults/Extensions.cs)

#### 1. Add Required Using Statement
```csharp
using OpenTelemetry.Logs;  // Required for logging.AddOtlpExporter()
```

#### 2. Complete ConfigureOpenTelemetry Method
```csharp
public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
{
    var resourceBuilder = ResourceBuilder.CreateDefault()
        .AddService(serviceName: builder.Environment.ApplicationName);

    // Check if OTLP endpoint is configured
    var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

    // Configure SSL bypass for development HTTPS with self-signed certificates
    if (!string.IsNullOrWhiteSpace(otlpEndpoint) && builder.Environment.IsDevelopment())
    {
        // Allow untrusted certificates for localhost gRPC connections in development
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        builder.Services.Configure<OtlpExporterOptions>(otlpOptions =>
        {
            otlpOptions.HttpClientFactory = () => new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        });
    }

    // Setup logging to be exported via OpenTelemetry
    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
        logging.SetResourceBuilder(resourceBuilder);

        // ✅ CRITICAL: Add OTLP exporter for logging
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            logging.AddOtlpExporter();
        }
    });

    var otel = builder.Services.AddOpenTelemetry();

    otel.WithMetrics(metrics =>
    {
        metrics.SetResourceBuilder(resourceBuilder);
        metrics.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddRuntimeInstrumentation()
               .AddMeter("Microsoft.Orleans");

        // ✅ CRITICAL: Add OTLP exporter for metrics
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter();
        }
    });

    otel.WithTracing(tracing =>
    {
        tracing.SetResourceBuilder(resourceBuilder);
        tracing
            .AddSource(builder.Environment.ApplicationName)
            .AddAspNetCoreInstrumentation(tracing =>
                // Don't trace requests to the health endpoint to avoid filling the dashboard with noise
                tracing.Filter = httpContext =>
                    !(httpContext.Request.Path.StartsWithSegments(HealthEndpointPath)
                      || httpContext.Request.Path.StartsWithSegments(AlivenessEndpointPath))
            )
            .AddHttpClientInstrumentation()
            .AddSource("Microsoft.Orleans.Application")
            .AddSource("Microsoft.Orleans.Runtime");

        // ✅ CRITICAL: Add OTLP exporter for tracing
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter();
        }
    });

    return builder;
}
```

## Key Changes

### Before (BROKEN)
```csharp
// Logging had NO exporter - structured logs couldn't reach dashboard
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

var otel = builder.Services.AddOpenTelemetry();
// ... metrics and tracing config ...

// ❌ WRONG: Cross-cutting exporter doesn't work with signal-specific exporters
otel.UseOtlpExporter();
```

### After (WORKING)
```csharp
// ✅ Logging has its own OTLP exporter
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.SetResourceBuilder(resourceBuilder);
    logging.AddOtlpExporter();  // ← CRITICAL FIX
});

var otel = builder.Services.AddOpenTelemetry();

otel.WithMetrics(metrics =>
{
    // ... metrics config ...
    metrics.AddOtlpExporter();  // ← Signal-specific exporter
});

otel.WithTracing(tracing =>
{
    // ... tracing config ...
    tracing.AddOtlpExporter();  // ← Signal-specific exporter
});

// ❌ DO NOT use otel.UseOtlpExporter() when using signal-specific exporters
```

## Why This Fix Works

OpenTelemetry in .NET has **separate pipelines** for the three telemetry signals:

1. **Logging Pipeline**: `builder.Logging.AddOpenTelemetry()` → needs `logging.AddOtlpExporter()`
2. **Metrics Pipeline**: `otel.WithMetrics()` → needs `metrics.AddOtlpExporter()`
3. **Tracing Pipeline**: `otel.WithTracing()` → needs `tracing.AddOtlpExporter()`

### Two Exporter Patterns

| Pattern | Usage | Limitation |
|---------|-------|------------|
| **Cross-cutting**: `otel.UseOtlpExporter()` | Applies to metrics + tracing only | ❌ Does NOT include logging |
| **Signal-specific**: `logging/metrics/tracing.AddOtlpExporter()` | Each signal gets its own exporter | ✅ All three signals work |

**Critical Rule**: You **cannot mix** both patterns - pick one:
- Use `UseOtlpExporter()` for metrics+tracing (but logging won't work)
- Use signal-specific exporters for all three (recommended)

## Testing the Fix

Run the Aspire application:
```bash
dotnet run --project OrleansVoting.AppHost
```

Open the Aspire Dashboard and verify:
- ✅ **Structured Logs** tab shows formatted log messages with properties
- ✅ **Traces** tab shows distributed traces across services
- ✅ **Metrics** tab shows performance counters and custom metrics

## Additional Notes

### SSL Certificate Handling
The configuration includes SSL bypass for development:
```csharp
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

builder.Services.Configure<OtlpExporterOptions>(otlpOptions =>
{
    otlpOptions.HttpClientFactory = () => new HttpClient(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });
});
```

This is necessary because:
- Aspire Dashboard uses gRPC over HTTPS with self-signed certificates
- Without this, OTLP exporter fails with SSL validation errors
- ⚠️ **Only use in Development environment** - production should use valid certificates

### Environment Variables
Aspire automatically sets `OTEL_EXPORTER_OTLP_ENDPOINT` to the dashboard's OTLP endpoint (e.g., `https://localhost:21244`). No manual configuration needed.

## References
- [Microsoft Learn: OpenTelemetry with OTLP and Aspire Dashboard](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-otlp-example)
- [Aspire Telemetry Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/telemetry)
- [OpenTelemetry .NET Logging](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md#enable-log-exporter)

## File Changes Summary

**Modified**: `OrleansVoting.ServiceDefaults/Extensions.cs`
- Added `using OpenTelemetry.Logs;`
- Added `logging.AddOtlpExporter()` to logging configuration
- Added `metrics.AddOtlpExporter()` to metrics configuration
- Added `tracing.AddOtlpExporter()` to tracing configuration
- Removed conflicting `otel.UseOtlpExporter()` call
- Added ResourceBuilder to all three signals
- Added SSL certificate bypass for development gRPC connections
