using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Hellnet.Observability;

/// <summary>
/// Default resilience strategies for OTLP collector interactions.
/// Uses Polly v8 with a pipeline approach (retry + circuit breaker + timeout).
/// </summary>
public static class HellnetResilience
{
    /// <summary>
    /// Default timeout for OTLP collector operations (5 seconds).
    /// Prevents the application from hanging if the collector is unreachable.
    /// </summary>
    public static TimeSpan TimeoutDuration { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Number of retry attempts before giving up (default: 3).
    /// Includes the initial attempt, so 3 retries = 4 total attempts.
    /// </summary>
    public static int RetryCount { get; set; } = 3;

    /// <summary>
    /// Delay between retries (default: 100ms base with exponential backoff).
    /// Actual delays: 100ms, 200ms, 400ms.
    /// </summary>
    public static TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Opens the circuit after this many consecutive failures (default: 5).
    /// </summary>
    public static int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Time before attempting to close the circuit (default: 30 seconds).
    /// </summary>
    public static TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default resilience pipeline for OTLP collector operations.
    /// Combines: timeout → retry → circuit breaker.
    /// </summary>
    public static ResiliencePipeline DefaultPipeline { get; } = BuildDefaultPipeline();

    /// <summary>
    /// Creates a resilience pipeline for OTLP health check operations.
    /// Uses fewer retries (2) and shorter timeout (3s) for fast-failing health probes.
    /// </summary>
    public static ResiliencePipeline HealthCheckPipeline { get; } = BuildHealthCheckPipeline();

    private static ResiliencePipeline BuildDefaultPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = _ => ValueTask.FromResult(true),
                MaxRetryAttempts = RetryCount,
                Delay = RetryBaseDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = _ => ValueTask.FromResult(true),
                FailureRatio = 1.0, // any failure counts
                MinimumThroughput = CircuitBreakerFailureThreshold,
                BreakDuration = CircuitBreakerBreakDuration,
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeoutDuration,
            })
            .Build();
    }

    private static ResiliencePipeline BuildHealthCheckPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = _ => ValueTask.FromResult(true),
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(50),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(3),
            })
            .Build();
    }
}
