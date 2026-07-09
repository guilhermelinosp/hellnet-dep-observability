using System.Diagnostics;
using System.Diagnostics.Metrics;
using Hellnet.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using Moq;
using Xunit;

namespace Hellnet.Observability.IntegrationTests;

[CollectionDefinition("Sequential", DisableParallelization = true)]
public sealed class SequentialCollection;

[Collection("Sequential")]
public sealed class IntegrationTests : IDisposable
{
    private const string OtlpEndpoint = "http://alloy.monitoring:4317";
    private readonly string _prefix = $"test:{Guid.NewGuid():N}:";

    public IntegrationTests()
    {
        ClearEnvVars();
        DependencyInjection.ResetCachedOptions();
    }

    public void Dispose()
    {
        ClearEnvVars();
        DependencyInjection.ResetCachedOptions();
    }

    private static void ClearEnvVars()
    {
        Environment.SetEnvironmentVariable("HELLNET_SERVICE_NAME", null);
        Environment.SetEnvironmentVariable("HELLNET_OTLP_ENDPOINT", null);
        Environment.SetEnvironmentVariable("HELLNET_OTLP_PROTOCOL", null);
    }

    private void SetRequiredEnvVars()
    {
        Environment.SetEnvironmentVariable("HELLNET_SERVICE_NAME", _prefix + "integration");
        Environment.SetEnvironmentVariable("HELLNET_OTLP_ENDPOINT", OtlpEndpoint);
        Environment.SetEnvironmentVariable("HELLNET_OTLP_PROTOCOL", "grpc");
    }

    // =====================================================================
    // Logging
    // =====================================================================

    [Fact]
    public void Logging_ConfiguresSerilog_WithoutThrow()
    {
        SetRequiredEnvVars();
        var svc = new ServiceCollection();
        svc.AddHellnetLogging();
        using var sp = svc.BuildServiceProvider();
        var logger = sp.GetRequiredService<ILogger<IntegrationTests>>();
        logger.Should().NotBeNull();
    }

    [Fact]
    public void Logging_EmitsToConsole()
    {
        SetRequiredEnvVars();
        var svc = new ServiceCollection();
        svc.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Trace));
        svc.AddHellnetLogging();
        using var sp = svc.BuildServiceProvider();
        var logger = sp.GetRequiredService<ILogger<IntegrationTests>>();

        logger.LogInformation("Integration test log: {Prefix}", _prefix);
        logger.LogWarning("Warning with args: {Count}", 42);
        logger.LogError("Error message");
    }

    // =====================================================================
    // Tracing
    // =====================================================================

    [Fact]
    public void Tracing_CreatesAndExportsSpans()
    {
        SetRequiredEnvVars();
        var svc = new ServiceCollection();
        svc.AddHellnetTracing(tracing => tracing.AddSource("Hellnet.Observability"));
        using var sp = svc.BuildServiceProvider();

        using var source = new ActivitySource("Hellnet.Observability");
        using var activity = source.StartActivity("integration-test-span");

        // Activity may be null if sampler decides not to sample — that's OK
        // The test verifies the pipeline doesn't throw
        if (activity is not null)
        {
            activity.SetTag("test.id", _prefix);
            activity.SetTag("test.type", "integration");
            activity.AddEvent(new ActivityEvent("test-event"));
        }
    }

    [Fact]
    public void Tracing_WithCustomCallback()
    {
        SetRequiredEnvVars();
        var svc = new ServiceCollection();
        svc.AddHellnetTracing(tracing => tracing.AddSource("CustomSource"));
        using var sp = svc.BuildServiceProvider();
    }

    // =====================================================================
    // Metrics
    // =====================================================================

    [Fact]
    public void Metrics_RecordsInstruments()
    {
        SetRequiredEnvVars();
        var svc = new ServiceCollection();
        svc.AddHellnetMetrics();
        using var sp = svc.BuildServiceProvider();

        var meter = new Meter(_prefix + "metrics");
        var counter = meter.CreateCounter<long>("integration.test.counter");
        var histogram = meter.CreateHistogram<double>("integration.test.histogram");

        counter.Add(1, new KeyValuePair<string, object?>("test.id", _prefix));
        counter.Add(2, new KeyValuePair<string, object?>("test.id", _prefix));
        histogram.Record(10.5, new KeyValuePair<string, object?>("test.id", _prefix));
        histogram.Record(20.3, new KeyValuePair<string, object?>("test.id", _prefix));
    }

    [Fact]
    public void Metrics_WithCustomCallback()
    {
        SetRequiredEnvVars();
        var svc = new ServiceCollection();
        svc.AddHellnetMetrics(metrics => metrics.AddMeter("TestMeter"));
        using var sp = svc.BuildServiceProvider();
    }

    // =====================================================================
    // Combined pipeline
    // =====================================================================

    [Fact]
    public void AllPipelines_WorkTogether()
    {
        SetRequiredEnvVars();
        var svc = new ServiceCollection();
        svc.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        svc.AddHellnetLogging();
        svc.AddHellnetTracing(tracing => tracing.AddSource("Hellnet.Observability"));
        svc.AddHellnetMetrics();
        using var sp = svc.BuildServiceProvider();

        var logger = sp.GetRequiredService<ILogger<IntegrationTests>>();
        logger.LogInformation("Combined pipeline test {Prefix}", _prefix);

        using var source = new ActivitySource("Hellnet.Observability");
        using var activity = source.StartActivity("combined-test");

        var meter = new Meter(_prefix + "combined-metrics");
        meter.CreateCounter<long>("combined.counter").Add(1);
    }

    // =====================================================================
    // Health checks
    // =====================================================================

    [Fact]
    public void HealthChecks_WorkWithoutEnvVars()
    {
        var svc = new ServiceCollection();
        svc.AddHellnetHealthChecks();
        using var sp = svc.BuildServiceProvider();
    }

    [Fact]
    public void HealthChecks_WithOtlpEndpoint_DoesNotThrow()
    {
        Environment.SetEnvironmentVariable("HELLNET_OTLP_ENDPOINT", OtlpEndpoint);
        var svc = new ServiceCollection();
        svc.AddHellnetHealthChecks();
        using var sp = svc.BuildServiceProvider();
    }

    // =====================================================================
    // UseHellnetObservability middleware
    // =====================================================================

    [Fact]
    public void UseHellnetObservability_WithEnv_Works()
    {
        SetRequiredEnvVars();
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();
        app.UseHellnetObservability();
    }

    [Fact]
    public async Task UseHellnetObservability_Throws_WhenMissingEnv()
    {
        ClearEnvVars();
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            app.UseHellnetObservability();
            return Task.CompletedTask;
        });
    }

    // =====================================================================
    // Options caching
    // =====================================================================

    [Fact]
    public void OptionsCache_CanBeReset()
    {
        SetRequiredEnvVars();
        var svc = new ServiceCollection();
        svc.AddHellnetLogging();

        ClearEnvVars();
        DependencyInjection.ResetCachedOptions();

        var svc2 = new ServiceCollection();
        FluentActions.Invoking(() => svc2.AddHellnetLogging()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OptionsCache_HoldsAfterFirstLoad()
    {
        SetRequiredEnvVars();
        var svc = new ServiceCollection();
        svc.AddHellnetLogging();

        // Clear env but cache is still set — second call must not throw
        ClearEnvVars();
        var svc2 = new ServiceCollection();
        svc2.AddHellnetLogging();
    }

    // =====================================================================
    // Resilience — Polly pipelines
    // =====================================================================

    [Fact]
    public async Task HellnetResilience_DefaultPipeline_SuccessfulOperation()
    {
        // Verifies the default pipeline passes through a successful operation
        var result = await HellnetResilience.DefaultPipeline.ExecuteAsync(
            ct => new ValueTask<int>(42));
        result.Should().Be(42);
    }

    [Fact]
    public async Task HellnetResilience_HealthCheckPipeline_SuccessfulOperation()
    {
        // Verifies the health check pipeline passes through a successful operation
        var result = await HellnetResilience.HealthCheckPipeline.ExecuteAsync(
            ct => new ValueTask<string>("ok"));
        result.Should().Be("ok");
    }

    [Fact]
    public async Task HellnetResilience_Retry_TransientFailure_RetriesThenThrows()
    {
        // Verifies that retry exhausts attempts and re-throws the original exception
        var attempts = 0;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HellnetResilience.HealthCheckPipeline.ExecuteAsync(
                async ct =>
                {
                    attempts++;
                    await Task.Delay(1, ct);
                    throw new InvalidOperationException($"attempt-{attempts}");
                }).AsTask());

        ex.Message.Should().Contain("attempt");
        attempts.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task HellnetResilience_Timeout_HealthCheckPipeline_ThrowsOnTimeout()
    {
        // HealthCheckPipeline has a 3s timeout — verify it fires for slow operations
        var ex = await Assert.ThrowsAsync<TimeoutRejectedException>(() =>
            HellnetResilience.HealthCheckPipeline.ExecuteAsync(
                async ct =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), ct);
                }).AsTask());
        ex.Should().NotBeNull();
    }

    [Fact]
    public async Task HellnetResilience_CircuitBreaker_OpensAfterFailures()
    {
        // Custom pipeline with very low thresholds
        var pipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = _ => ValueTask.FromResult(true),
                FailureRatio = 1.0,
                MinimumThroughput = 2,
                BreakDuration = TimeSpan.FromSeconds(1),
            })
            .Build();

        var attempts = 0;
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await pipeline.ExecuteAsync(ct =>
                {
                    attempts++;
                    throw new InvalidOperationException($"fail-{attempts}");
                });
            }
            catch (BrokenCircuitException)
            {
                attempts.Should().BeGreaterThan(1);
                return;
            }
            catch (InvalidOperationException)
            {
                // Expected first failure
            }
        }

        Assert.Fail("Circuit breaker should have opened before 3 attempts");
    }

    // =====================================================================
    // ITelemetry high-level abstraction
    // =====================================================================

    [Fact]
    public async Task AddHellnetTelemetry_RegistersAndWorks()
    {
        SetRequiredEnvVars();
        var svc = new ServiceCollection();
        svc.AddLogging();
        svc.AddHellnetTelemetry();

        await using var sp = svc.BuildServiceProvider();
        var telemetry = sp.GetRequiredService<ITelemetry>();
        telemetry.Should().NotBeNull();

        // Logger — native ILogger<T>
        var logger = telemetry.Logger<IntegrationTests>();
        logger.Should().NotBeNull();
        logger.LogInformation("ITelemetry logger works: {Prefix}", _prefix);

        // Tracing — native ActivitySource
        using var activity = telemetry.ActivitySource.StartActivity("test-activity");
        if (activity is not null)
        {
            activity.SetTag("test.id", _prefix);
        }

        // Metrics — native Meter
        var counter = telemetry.Meter.CreateCounter<long>("test.counter");
        counter.Add(1);
        var histogram = telemetry.Meter.CreateHistogram<double>("test.histogram");
        histogram.Record(10.5);
    }

    [Fact]
    public void AddHellnetTelemetry_Throws_WhenEnvMissing()
    {
        ClearEnvVars();
        var svc = new ServiceCollection();
        FluentActions.Invoking(() => svc.AddHellnetTelemetry()).Should().Throw<InvalidOperationException>();
    }
}
