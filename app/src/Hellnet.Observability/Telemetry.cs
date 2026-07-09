using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hellnet.Observability;

/// <summary>
/// High-level abstraction over tracing, metrics, and logging.
/// No OpenTelemetry knowledge required — just call methods.
/// </summary>
public interface ITelemetry
{
    /// <summary>Start a span/activity for distributed tracing.</summary>
    Activity? StartActivity(string name);

    /// <summary>Increment a counter metric by a value (default 1).</summary>
    void Count(string name, long value = 1);

    /// <summary>Record a value in a histogram metric.</summary>
    void Record(string name, double value);

    /// <summary>Get a typed logger for structured logging.</summary>
    ILogger<T> Logger<T>();
}

/// <summary>
/// Default implementation backed by OpenTelemetry ActivitySource and Meter.
/// Automatically registered via <c>AddHellnetTelemetry()</c>.
/// </summary>
internal sealed class HellnetTelemetry : ITelemetry
{
    private readonly ActivitySource _source;
    private readonly Meter _meter;
    private readonly ILoggerFactory _loggerFactory;

    private readonly ConcurrentDictionary<string, Counter<long>> _counters = new();
    private readonly ConcurrentDictionary<string, Histogram<double>> _histograms = new();

    public HellnetTelemetry(string serviceName, ILoggerFactory loggerFactory)
    {
        _source = new ActivitySource(serviceName);
        _meter = new Meter(serviceName);
        _loggerFactory = loggerFactory;
    }

    public Activity? StartActivity(string name)
        => _source.StartActivity(name);

    public void Count(string name, long value = 1)
    {
        var counter = _counters.GetOrAdd(name, n => _meter.CreateCounter<long>(n));
        counter.Add(value);
    }

    public void Record(string name, double value)
    {
        var histogram = _histograms.GetOrAdd(name, n => _meter.CreateHistogram<double>(n));
        histogram.Record(value);
    }

    public ILogger<T> Logger<T>()
        => _loggerFactory.CreateLogger<T>();
}
