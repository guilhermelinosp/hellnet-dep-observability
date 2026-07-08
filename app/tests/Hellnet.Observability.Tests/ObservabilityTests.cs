using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using OpenTelemetry.Exporter;

using Serilog.Events;

using Xunit;

namespace Hellnet.Observability.Tests;

[CollectionDefinition("Sequential", DisableParallelization = true)]
public sealed class SequentialCollection;

[Collection("Sequential")]
public sealed class ObservabilityTests : IDisposable
{
    public ObservabilityTests()
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
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
    }

    private static void SetRequiredEnvVars()
    {
        Environment.SetEnvironmentVariable("HELLNET_SERVICE_NAME", "test-service");
        Environment.SetEnvironmentVariable("HELLNET_OTLP_ENDPOINT", "http://localhost:4317");
        Environment.SetEnvironmentVariable("HELLNET_OTLP_PROTOCOL", "grpc");
    }

    // =====================================================================
    // ValidateOptions
    // =====================================================================

    [Fact]
    public void ValidateOptions_Throws_WhenAllMissing()
    {
        var options = new HellnetObservabilityOptions();
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => DependencyInjection.ValidateOptions(options));

        Assert.Contains("HELLNET_SERVICE_NAME", ex.Message);
        Assert.Contains("HELLNET_OTLP_ENDPOINT", ex.Message);
        Assert.Contains("HELLNET_OTLP_PROTOCOL", ex.Message);
    }

    [Fact]
    public void ValidateOptions_Throws_WhenServiceNameMissing()
    {
        var options = new HellnetObservabilityOptions
        {
            OtlpEndpoint = "http://localhost:4317",
            OtlpProtocol = "grpc"
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => DependencyInjection.ValidateOptions(options));

        Assert.Contains("HELLNET_SERVICE_NAME", ex.Message);
    }

    [Fact]
    public void ValidateOptions_Throws_WhenEndpointMissing()
    {
        var options = new HellnetObservabilityOptions
        {
            ServiceName = "test",
            OtlpProtocol = "grpc"
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => DependencyInjection.ValidateOptions(options));

        Assert.Contains("HELLNET_OTLP_ENDPOINT", ex.Message);
    }

    [Fact]
    public void ValidateOptions_Throws_WhenProtocolMissing()
    {
        var options = new HellnetObservabilityOptions
        {
            ServiceName = "test",
            OtlpEndpoint = "http://localhost:4317"
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => DependencyInjection.ValidateOptions(options));

        Assert.Contains("HELLNET_OTLP_PROTOCOL", ex.Message);
    }

    [Fact]
    public void ValidateOptions_Throws_WhenEndpointIsInvalidUri()
    {
        var options = new HellnetObservabilityOptions
        {
            ServiceName = "test",
            OtlpEndpoint = "not-a-valid-uri",
            OtlpProtocol = "grpc"
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => DependencyInjection.ValidateOptions(options));

        Assert.Contains("Invalid HELLNET_OTLP_ENDPOINT", ex.Message);
        Assert.Contains("not-a-valid-uri", ex.Message);
    }

    [Fact]
    public void ValidateOptions_Throws_WhenProtocolInvalid()
    {
        var options = new HellnetObservabilityOptions
        {
            ServiceName = "test",
            OtlpEndpoint = "http://localhost:4317",
            OtlpProtocol = "invalid-value"
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => DependencyInjection.ValidateOptions(options));

        Assert.Contains("Expected 'grpc' or 'http'", ex.Message);
    }

    [Fact]
    public void ValidateOptions_Success_WhenAllValid()
    {
        var options = new HellnetObservabilityOptions
        {
            ServiceName = "test",
            OtlpEndpoint = "http://localhost:4317",
            OtlpProtocol = "grpc"
        };

        DependencyInjection.ValidateOptions(options);
    }

    // =====================================================================
    // ParseProtocol
    // =====================================================================

    [Theory]
    [InlineData("http", OtlpExportProtocol.HttpProtobuf)]
    [InlineData("HTTP", OtlpExportProtocol.HttpProtobuf)]
    [InlineData("Http", OtlpExportProtocol.HttpProtobuf)]
    [InlineData("http/protobuf", OtlpExportProtocol.HttpProtobuf)]
    [InlineData("httpproto", OtlpExportProtocol.HttpProtobuf)]
    [InlineData("grpc", OtlpExportProtocol.Grpc)]
    [InlineData("GRPC", OtlpExportProtocol.Grpc)]
    [InlineData("gRPC", OtlpExportProtocol.Grpc)]
    [InlineData("grpc/protobuf", OtlpExportProtocol.Grpc)]
    [InlineData(" grpc ", OtlpExportProtocol.Grpc)]
    [InlineData("  http  ", OtlpExportProtocol.HttpProtobuf)]
    public void ParseProtocol_ValidValues(string input, OtlpExportProtocol expected)
    {
        OtlpExportProtocol result = DependencyInjection.ParseProtocol(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseProtocol_Throws_WhenInvalid()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => DependencyInjection.ParseProtocol("invalid"));

        Assert.Contains("Expected 'grpc' or 'http'", ex.Message);
        Assert.Contains("invalid", ex.Message);
    }

    // =====================================================================
    // GetDotNetEnvironment
    // =====================================================================

    [Fact]
    public void GetDotNetEnvironment_ReturnsProduction_WhenNoEnvSet()
    {
        var result = DependencyInjection.GetDotNetEnvironment();
        Assert.Equal("Production", result);
    }

    [Fact]
    public void GetDotNetEnvironment_UsesDOTNET_ENVIRONMENT()
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Staging");
        var result = DependencyInjection.GetDotNetEnvironment();
        Assert.Equal("Staging", result);
    }

    [Fact]
    public void GetDotNetEnvironment_UsesASPNETCORE_ENVIRONMENT_WhenDOTNET_ENVIRONMENTMissing()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        var result = DependencyInjection.GetDotNetEnvironment();
        Assert.Equal("Development", result);
    }

    [Fact]
    public void GetDotNetEnvironment_DOTNET_ENVIRONMENT_TakesPrecedence()
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Production");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        var result = DependencyInjection.GetDotNetEnvironment();
        Assert.Equal("Production", result);
    }

    // =====================================================================
    // ParseLogLevel
    // =====================================================================

    [Theory]
    [InlineData("Debug", LogEventLevel.Debug)]
    [InlineData("Information", LogEventLevel.Information)]
    [InlineData("Warning", LogEventLevel.Warning)]
    [InlineData("Error", LogEventLevel.Error)]
    [InlineData("Fatal", LogEventLevel.Fatal)]
    [InlineData("", LogEventLevel.Information)]
    [InlineData(null, LogEventLevel.Information)]
    [InlineData("invalid", LogEventLevel.Information)]
    public void ParseLogLevel_ReturnsExpected(string? input, LogEventLevel expected)
    {
        LogEventLevel result = DependencyInjection.ParseLogLevel(input, LogEventLevel.Information);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseLogLevel_UsesDefault_WhenValueIsNull()
    {
        LogEventLevel result = DependencyInjection.ParseLogLevel(null, LogEventLevel.Error);
        Assert.Equal(LogEventLevel.Error, result);
    }

    // =====================================================================
    // AddHellnetLogging — env missing (throw)
    // =====================================================================

    [Fact]
    public void AddHellnetLogging_Throws_WhenAllEnvMissing()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services.AddHellnetLogging());
    }

    [Fact]
    public void AddHellnetLogging_Throws_WhenEndpointIsInvalidUri()
    {
        Environment.SetEnvironmentVariable("HELLNET_SERVICE_NAME", "test");
        Environment.SetEnvironmentVariable("HELLNET_OTLP_ENDPOINT", "not-a-valid-uri");
        Environment.SetEnvironmentVariable("HELLNET_OTLP_PROTOCOL", "grpc");

        var services = new ServiceCollection();
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => services.AddHellnetLogging());
        Assert.Contains("Invalid HELLNET_OTLP_ENDPOINT", ex.Message);
    }

    // =====================================================================
    // AddHellnetTracing — env missing (throw)
    // =====================================================================

    [Fact]
    public void AddHellnetTracing_Throws_WhenAllEnvMissing()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services.AddHellnetTracing());
    }

    // =====================================================================
    // AddHellnetMetrics — env missing (throw)
    // =====================================================================

    [Fact]
    public void AddHellnetMetrics_Throws_WhenAllEnvMissing()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services.AddHellnetMetrics());
    }

    // =====================================================================
    // AddHellnetLogging — success
    // =====================================================================

    [Fact]
    public void AddHellnetLogging_ReturnsServiceCollection()
    {
        SetRequiredEnvVars();
        var services = new ServiceCollection();
        IServiceCollection result = services.AddHellnetLogging();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddHellnetLogging_RegistersSerilog()
    {
        SetRequiredEnvVars();
        var services = new ServiceCollection();
        services.AddHellnetLogging();
        using ServiceProvider sp = services.BuildServiceProvider();
        ILogger<ObservabilityTests> logger = sp.GetRequiredService<ILogger<ObservabilityTests>>();
        Assert.NotNull(logger);
    }

    // =====================================================================
    // AddHellnetTracing — success
    // =====================================================================

    [Fact]
    public void AddHellnetTracing_ReturnsServiceCollection()
    {
        SetRequiredEnvVars();
        var services = new ServiceCollection();
        IServiceCollection result = services.AddHellnetTracing();
        Assert.Same(services, result);
    }

    // =====================================================================
    // AddHellnetMetrics — success
    // =====================================================================

    [Fact]
    public void AddHellnetMetrics_ReturnsServiceCollection()
    {
        SetRequiredEnvVars();
        var services = new ServiceCollection();
        IServiceCollection result = services.AddHellnetMetrics();
        Assert.Same(services, result);
    }

    // =====================================================================
    // Add all three together
    // =====================================================================

    [Fact]
    public void AllThreeExtensions_WorkTogether()
    {
        SetRequiredEnvVars();
        var services = new ServiceCollection();
        services.AddHellnetLogging();
        services.AddHellnetTracing();
        services.AddHellnetMetrics();
        using ServiceProvider sp = services.BuildServiceProvider();
        ILogger<ObservabilityTests> logger = sp.GetRequiredService<ILogger<ObservabilityTests>>();
        Assert.NotNull(logger);
    }

    // =====================================================================
    // Options caching
    // =====================================================================

    [Fact]
    public void Options_Cached_AfterFirstLoad()
    {
        SetRequiredEnvVars();
        var services = new ServiceCollection();
        services.AddHellnetLogging();

        // Clear env vars — second call must use cache, not re-read
        ClearEnvVars();
        var services2 = new ServiceCollection();
        services2.AddHellnetLogging();
    }

    [Fact]
    public void Options_Cache_CanBeReset()
    {
        SetRequiredEnvVars();
        var services = new ServiceCollection();
        services.AddHellnetLogging();

        // Reset cache + clear env
        ClearEnvVars();
        DependencyInjection.ResetCachedOptions();

        // Must re-validate and throw
        var services2 = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services2.AddHellnetLogging());
    }

    // =====================================================================
    // Callback extensibility
    // =====================================================================

    [Fact]
    public void AddHellnetTracing_WithCallback_DoesNotThrow()
    {
        SetRequiredEnvVars();
        var services = new ServiceCollection();
        services.AddHellnetTracing(tracing => tracing.AddSource("CustomSource"));
    }

    [Fact]
    public void AddHellnetMetrics_WithCallback_DoesNotThrow()
    {
        SetRequiredEnvVars();
        var services = new ServiceCollection();
        services.AddHellnetMetrics(metrics => metrics.AddMeter("TestMeter"));
    }

    [Fact]
    public void AddHellnetTracing_WithNullCallback_Works()
    {
        SetRequiredEnvVars();
        var services = new ServiceCollection();
        services.AddHellnetTracing(configureTracing: null);
        using ServiceProvider sp = services.BuildServiceProvider();
    }

    [Fact]
    public void AddHellnetMetrics_WithNullCallback_Works()
    {
        SetRequiredEnvVars();
        var services = new ServiceCollection();
        services.AddHellnetMetrics(configureMetrics: null);
        using ServiceProvider sp = services.BuildServiceProvider();
    }

    // =====================================================================
    // Log level env vars
    // =====================================================================

    [Fact]
    public void AddHellnetLogging_UsesDefaultLogLevels_WhenNoEnvOverride()
    {
        SetRequiredEnvVars();
        var services = new ServiceCollection();
        services.AddHellnetLogging();
        using ServiceProvider sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<ILogger<ObservabilityTests>>());
    }

    [Fact]
    public void AddHellnetLogging_AcceptsLogLevelEnvOverride()
    {
        Environment.SetEnvironmentVariable("HELLNET_LOG_LEVEL", "Debug");
        SetRequiredEnvVars();

        var services = new ServiceCollection();
        services.AddHellnetLogging();
        using ServiceProvider sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<ILogger<ObservabilityTests>>());
    }

    // =====================================================================
    // .env file loading
    // =====================================================================

    [Fact]
    public void LoadEnvFileIfDevelopment_Skips_WhenNotDevelopment()
    {
        // DOTNET_ENVIRONMENT already cleared → "Production" default
        DependencyInjection.LoadEnvFileIfDevelopment();
        // No exception expected — production guard works
    }

    [Fact]
    public void LoadEnvFileIfDevelopment_LoadsFile_WhenDevelopment()
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");

        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var envFile = Path.Combine(tempDir, "test.env");
            File.WriteAllText(envFile,
                "# comment line\n" +
                "HELLNET_SERVICE_NAME=from-env\n" +
                "HELLNET_OTLP_ENDPOINT=http://from-env:4317\n" +
                "HELLNET_OTLP_PROTOCOL=grpc\n" +
                "EMPTY=\n" +
                "QUOTED=\"value\"\n" +
                "SINGLE_QUOTED='value2'\n");

            Environment.SetEnvironmentVariable("HELLNET_ENV_FILE", envFile);
            DependencyInjection.ResetCachedOptions();
            DependencyInjection.LoadEnvFileIfDevelopment();

            Assert.Equal("from-env", Environment.GetEnvironmentVariable("HELLNET_SERVICE_NAME"));
            Assert.Equal("http://from-env:4317", Environment.GetEnvironmentVariable("HELLNET_OTLP_ENDPOINT"));
            Assert.Equal("grpc", Environment.GetEnvironmentVariable("HELLNET_OTLP_PROTOCOL"));
            Assert.Equal("value", Environment.GetEnvironmentVariable("QUOTED"));
            Assert.Equal("value2", Environment.GetEnvironmentVariable("SINGLE_QUOTED"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadEnvFileIfDevelopment_DoesNotOverrideExistingEnv()
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("HELLNET_SERVICE_NAME", "pre-set");

        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var envFile = Path.Combine(tempDir, "test.env");
            File.WriteAllText(envFile, "HELLNET_SERVICE_NAME=from-file\n");

            Environment.SetEnvironmentVariable("HELLNET_ENV_FILE", envFile);
            DependencyInjection.ResetCachedOptions();
            DependencyInjection.LoadEnvFileIfDevelopment();

            // Should keep the pre-set value, not override
            Assert.Equal("pre-set", Environment.GetEnvironmentVariable("HELLNET_SERVICE_NAME"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // =====================================================================
    // UseHellnetObservability
    // =====================================================================

    [Fact]
    public void UseHellnetObservability_Throws_WhenAllEnvMissing()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        WebApplication app = builder.Build();
        Assert.Throws<InvalidOperationException>(() => app.UseHellnetObservability());
    }

    [Fact]
    public void UseHellnetObservability_Success_WhenEnvSet()
    {
        SetRequiredEnvVars();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        WebApplication app = builder.Build();
        app.UseHellnetObservability();
    }

    // =====================================================================
    // AddHellnetHealthChecks
    // =====================================================================

    [Fact]
    public void AddHellnetHealthChecks_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        IServiceCollection result = services.AddHellnetHealthChecks();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddHellnetHealthChecks_WithCallback_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddHellnetHealthChecks(checks => checks.AddCheck("custom", () => HealthCheckResult.Healthy()));
    }

    [Fact]
    public void AddHellnetHealthChecks_WithNullCallback_Works()
    {
        var services = new ServiceCollection();
        services.AddHellnetHealthChecks(configureHealthChecks: null);
        using ServiceProvider sp = services.BuildServiceProvider();
    }

    [Fact]
    public void AddHellnetHealthChecks_Works_WithoutEnvVars()
    {
        // No env vars set — should still work (OTLP check is optional)
        ClearEnvVars();
        var services = new ServiceCollection();
        services.AddHellnetHealthChecks();
        using ServiceProvider sp = services.BuildServiceProvider();
    }

    [Fact]
    public void AddHellnetHealthChecks_RegistersOtlpCheck_WhenEndpointEnvSet()
    {
        SetRequiredEnvVars();
        var services = new ServiceCollection();
        services.AddHellnetHealthChecks();
        using ServiceProvider sp = services.BuildServiceProvider();
    }

    // =====================================================================
    // UseHellnetHealthChecks
    // =====================================================================

    [Fact]
    public void UseHellnetHealthChecks_DoesNotThrow_WhenRegistered()
    {
        SetRequiredEnvVars();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddHellnetHealthChecks();
        WebApplication app = builder.Build();
        app.UseHellnetHealthChecks();
    }

    [Fact]
    public void UseHellnetHealthChecks_Works_WithoutOtlpEnv()
    {
        ClearEnvVars();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddHellnetHealthChecks();
        WebApplication app = builder.Build();
        app.UseHellnetHealthChecks();
    }

    // =====================================================================
    // Health check predicates
    // =====================================================================

    [Fact]
    public void UseHellnetHealthChecks_Predicates_FilterCorrectly()
    {
        var liveReg = new HealthCheckRegistration(
            "self", s_passthroughCheck, null, ["live"]);
        var readyReg = new HealthCheckRegistration(
            "otel-collector", s_passthroughCheck, null, ["ready"]);
        var untaggedReg = new HealthCheckRegistration(
            "other", s_passthroughCheck, null, null);

        Assert.True(DependencyInjection.IsLiveCheck(liveReg));
        Assert.False(DependencyInjection.IsLiveCheck(readyReg));
        Assert.False(DependencyInjection.IsLiveCheck(untaggedReg));

        Assert.True(DependencyInjection.IsReadyCheck(readyReg));
        Assert.False(DependencyInjection.IsReadyCheck(liveReg));
        Assert.False(DependencyInjection.IsReadyCheck(untaggedReg));

        Assert.True(DependencyInjection.IsAllCheck(liveReg));
        Assert.True(DependencyInjection.IsAllCheck(readyReg));
        Assert.True(DependencyInjection.IsAllCheck(untaggedReg));
    }

    // =====================================================================
    // WriteJsonHealthReport
    // =====================================================================

    [Fact]
    public async Task WriteJsonHealthReport_ReturnsCorrectJson()
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["self"] = new HealthReportEntry(
                HealthStatus.Healthy, "OK", TimeSpan.Zero, null, null),
            ["custom"] = new HealthReportEntry(
                HealthStatus.Unhealthy, "Failed",
                TimeSpan.FromMilliseconds(100),
                new Exception("test error"), null)
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(50));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await DependencyInjection.WriteJsonHealthReport(context, report);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();

        Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Unhealthy", doc.RootElement.GetProperty("status").GetString());
        JsonElement checks = doc.RootElement.GetProperty("checks");
        Assert.Equal(2, checks.GetArrayLength());
        Assert.Equal("self", checks[0].GetProperty("name").GetString());
        Assert.Equal("custom", checks[1].GetProperty("name").GetString());
        Assert.NotNull(checks[1].GetProperty("error").GetString());
    }

    // =====================================================================
    // OtlpEndpointHealthCheck via HealthCheckService
    // =====================================================================

    [Fact]
    public async Task AddHellnetHealthChecks_OtlpCheck_ReportsUnhealthy_WhenCollectorUnreachable()
    {
        Environment.SetEnvironmentVariable("HELLNET_OTLP_ENDPOINT", "http://127.0.0.1:1");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHellnetHealthChecks();
        using ServiceProvider sp = services.BuildServiceProvider();
        HealthCheckService healthCheckService = sp.GetRequiredService<HealthCheckService>();

        HealthReport report = await healthCheckService.CheckHealthAsync(
            static check => check.Tags.Contains("ready"));

        Assert.Contains("otel-collector", report.Entries.Keys);
        Assert.Equal(HealthStatus.Unhealthy, report.Entries["otel-collector"].Status);
    }

    private static readonly PassthroughHealthCheck s_passthroughCheck = new();

    private sealed class PassthroughHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }
}
