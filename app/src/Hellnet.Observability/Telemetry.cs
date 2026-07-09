using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hellnet.Observability;

/// <summary>
/// Provides native OpenTelemetry primitives pre-configured for the service.
/// Use <see cref="ActivitySource"/> for tracing, <see cref="Meter"/> for metrics,
/// and <see cref="Logger{T}"/> for structured logging.
/// <para />
/// Register via <c>builder.Services.AddHellnetTelemetry()</c>.
/// </summary>
public interface ITelemetry
{
    /// <summary>Pre-registered ActivitySource (name = HELLNET_SERVICE_NAME).</summary>
    ActivitySource ActivitySource { get; }

    /// <summary>Pre-registered Meter (name = HELLNET_SERVICE_NAME).</summary>
    Meter Meter { get; }

    /// <summary>Typed logger backed by Serilog + OTLP.</summary>
    ILogger<T> Logger<T>();
}

internal sealed class HellnetTelemetry : ITelemetry
{
    public ActivitySource ActivitySource { get; }
    public Meter Meter { get; }
    private readonly ILoggerFactory _loggerFactory;

    public HellnetTelemetry(string serviceName, ILoggerFactory loggerFactory)
    {
        ActivitySource = new ActivitySource(serviceName);
        Meter = new Meter(serviceName);
        _loggerFactory = loggerFactory;
    }

    public ILogger<T> Logger<T>() => _loggerFactory.CreateLogger<T>();
}
