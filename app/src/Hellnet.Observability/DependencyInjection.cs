using System.Net.Sockets;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Formatting.Compact;

namespace Hellnet.Observability;

public sealed class HellnetObservabilityOptions
{
    public string ServiceName { get; set; } = string.Empty;
    public string OtlpEndpoint { get; set; } = string.Empty;
    public string OtlpProtocol { get; set; } = string.Empty;
}

public static class DependencyInjection
{
    public static IServiceCollection AddHellnetLogging(this IServiceCollection services)
    {
        HellnetObservabilityOptions options = LoadAndValidateOptions();
        ResourceBuilder resourceBuilder = CreateResourceBuilder(options);
        var otlpEndpoint = new Uri(options.OtlpEndpoint);
        ILogger logger = BuildSerilogLogger(options);
        services.AddSerilog(logger, dispose: true);
        services.ConfigureHellnetOtlpLogs(options, resourceBuilder, otlpEndpoint);
        return services;
    }

    public static IServiceCollection AddHellnetTracing(
        this IServiceCollection services,
        Action<TracerProviderBuilder>? configureTracing = null)
    {
        HellnetObservabilityOptions options = LoadAndValidateOptions();
        ResourceBuilder resourceBuilder = CreateResourceBuilder(options);
        var otlpEndpoint = new Uri(options.OtlpEndpoint);
        return services.AddHellnetTracing(options, resourceBuilder, otlpEndpoint, configureTracing);
    }

    public static IServiceCollection AddHellnetMetrics(
        this IServiceCollection services,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        HellnetObservabilityOptions options = LoadAndValidateOptions();
        ResourceBuilder resourceBuilder = CreateResourceBuilder(options);
        var otlpEndpoint = new Uri(options.OtlpEndpoint);
        return services.AddHellnetMetrics(options, resourceBuilder, otlpEndpoint, configureMetrics);
    }

    public static IApplicationBuilder UseHellnetObservability(this IApplicationBuilder app)
    {
        HellnetObservabilityOptions options = LoadAndValidateOptions();

        app.UseSerilogRequestLogging(loggingOptions =>
        {
            loggingOptions.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value);
            };
        });

        return app;
    }

    private static IServiceCollection AddHellnetTracing(
        this IServiceCollection services,
        HellnetObservabilityOptions options,
        ResourceBuilder resourceBuilder,
        Uri otlpEndpoint,
        Action<TracerProviderBuilder>? configureTracing = null)
    {
        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddOtlpExporter(otlp => ConfigureOtlpExporter(otlp, options, otlpEndpoint));

                configureTracing?.Invoke(tracing);
            });

        return services;
    }

    private static IServiceCollection AddHellnetMetrics(
        this IServiceCollection services,
        HellnetObservabilityOptions options,
        ResourceBuilder resourceBuilder,
        Uri otlpEndpoint,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddOtlpExporter(otlp => ConfigureOtlpExporter(otlp, options, otlpEndpoint));

                configureMetrics?.Invoke(metrics);
            });

        return services;
    }

    private static IServiceCollection ConfigureHellnetOtlpLogs(this IServiceCollection services, HellnetObservabilityOptions options, ResourceBuilder resourceBuilder, Uri otlpEndpoint)
    {
        services.AddOpenTelemetry()
            .WithLogging(logging => logging
                .SetResourceBuilder(resourceBuilder)
                .AddOtlpExporter(otlp => ConfigureOtlpExporter(otlp, options, otlpEndpoint)));

        return services;
    }

    private static ResourceBuilder CreateResourceBuilder(HellnetObservabilityOptions options) =>
        ResourceBuilder.CreateDefault()
            .AddService(options.ServiceName)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = GetDotNetEnvironment()
            });

    private static HellnetObservabilityOptions LoadOptionsFromEnvironment() =>
        new()
        {
            ServiceName = Environment.GetEnvironmentVariable("HELLNET_SERVICE_NAME") ?? string.Empty,
            OtlpEndpoint = Environment.GetEnvironmentVariable("HELLNET_OTLP_ENDPOINT") ?? string.Empty,
            OtlpProtocol = Environment.GetEnvironmentVariable("HELLNET_OTLP_PROTOCOL") ?? string.Empty
        };

    private static HellnetObservabilityOptions? _cachedOptions;
    private static bool _envFileLoaded;

    internal static void ResetCachedOptions()
    {
        _cachedOptions = null;
        _envFileLoaded = false;
    }

    internal static void LoadEnvFileIfDevelopment()
    {
        if (_envFileLoaded)
        {
            return;
        }

        _envFileLoaded = true;

        string env = GetDotNetEnvironment();
        if (env != "Development")
        {
            return;
        }

        string envFile = Environment.GetEnvironmentVariable("HELLNET_ENV_FILE")
            ?? Path.Combine(Directory.GetCurrentDirectory(), ".env");

        if (!File.Exists(envFile))
        {
            return;
        }

        foreach (string line in File.ReadAllLines(envFile))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            int eq = trimmed.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            string key = trimmed[..eq].Trim();
            string val = trimmed[(eq + 1)..].Trim();

            // Strip surrounding quotes
            if (val.Length >= 2 && val[0] is '"' or '\'' && val[0] == val[^1])
            {
                val = val[1..^1];
            }

            if (!string.IsNullOrWhiteSpace(key) && Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, val);
            }
        }
    }

    private static HellnetObservabilityOptions LoadAndValidateOptions()
    {
        HellnetObservabilityOptions? options = _cachedOptions;
        if (options is not null)
        {
            return options;
        }

        LoadEnvFileIfDevelopment();
        options = LoadOptionsFromEnvironment();
        ValidateOptions(options);
        _cachedOptions = options;
        return options;
    }

    internal static void ValidateOptions(HellnetObservabilityOptions options)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ServiceName))
        {
            missing.Add("HELLNET_SERVICE_NAME");
        }

        if (string.IsNullOrWhiteSpace(options.OtlpEndpoint))
        {
            missing.Add("HELLNET_OTLP_ENDPOINT");
        }

        if (string.IsNullOrWhiteSpace(options.OtlpProtocol))
        {
            missing.Add("HELLNET_OTLP_PROTOCOL");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Missing required environment variables: {string.Join(", ", missing)}");
        }

        if (!Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException($"Invalid HELLNET_OTLP_ENDPOINT. Must be a valid absolute URI. Got: {options.OtlpEndpoint}");
        }

        // Fail-fast: validate protocol value at startup, not lazily in OTel exporter
        ParseProtocol(options.OtlpProtocol);
    }

    internal static OtlpExportProtocol ParseProtocol(string protocolString)
        => protocolString.Trim().ToLowerInvariant() switch
        {
            "http" or "http/protobuf" or "httpproto" => OtlpExportProtocol.HttpProtobuf,
            "grpc" or "grpc/protobuf" => OtlpExportProtocol.Grpc,
            _ => throw new InvalidOperationException($"Invalid HELLNET_OTLP_PROTOCOL. Expected 'grpc' or 'http'. Got: {protocolString}")
        };

    private static void ConfigureOtlpExporter(OtlpExporterOptions exporterOptions, HellnetObservabilityOptions options, Uri otlpEndpoint)
    {
        exporterOptions.Endpoint = otlpEndpoint;
        exporterOptions.Protocol = ParseProtocol(options.OtlpProtocol);
    }

    internal static string GetDotNetEnvironment()
        => Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
           ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
           ?? "Production";


    internal static LogEventLevel ParseLogLevel(string? value, LogEventLevel defaultLevel)
        => Enum.TryParse(value, ignoreCase: true, out LogEventLevel level) ? level : defaultLevel;

    private static ILogger BuildSerilogLogger(HellnetObservabilityOptions options)
    {
        LogEventLevel defaultLevel = ParseLogLevel(
            Environment.GetEnvironmentVariable("HELLNET_LOG_LEVEL"),
            LogEventLevel.Information);

        LoggerConfiguration loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(defaultLevel)
            .Enrich.FromLogContext()
            .Enrich.WithExceptionDetails()
            .Enrich.WithEnvironmentUserName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("ServiceName", options.ServiceName)
            .Enrich.WithProperty("Environment", GetDotNetEnvironment());

        loggerConfiguration.WriteTo.Async(writeTo =>
            writeTo.Console(new RenderedCompactJsonFormatter()));

        return loggerConfiguration.CreateLogger();
    }

    // =====================================================================
    // Health Checks
    // =====================================================================

    /// <summary>
    /// Registers health checks: self (liveness) + OTLP collector TCP check (readiness).
    /// Use <see cref="UseHellnetHealthChecks"/> to map /live, /ready, /health endpoints.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureHealthChecks">
    /// Optional callback to add custom checks (e.g., database, cache, external APIs).
    /// Tag custom readiness checks with "ready" so they're included in /ready.
    /// </param>
    public static IServiceCollection AddHellnetHealthChecks(
        this IServiceCollection services,
        Action<IHealthChecksBuilder>? configureHealthChecks = null)
    {
        IHealthChecksBuilder builder = services.AddHealthChecks();

        // Self check — always healthy (for liveness probe)
        builder.AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

        // OTLP collector reachability check (for readiness probe)
        string? otlpEndpoint = Environment.GetEnvironmentVariable("HELLNET_OTLP_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(otlpEndpoint)
            && Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out Uri? uri))
        {
            builder.AddCheck("otel-collector", new OtlpEndpointHealthCheck(uri), tags: ["ready"]);
        }

        configureHealthChecks?.Invoke(builder);

        return services;
    }

    /// <summary>
    /// Maps health check endpoints: /live (liveness), /ready (readiness), /health (aggregate).
    /// Must be called after <see cref="AddHellnetHealthChecks"/>.
    /// </summary>
    public static IApplicationBuilder UseHellnetHealthChecks(this IApplicationBuilder app)
    {
        app.UseHealthChecks("/live", new HealthCheckOptions
        {
            Predicate = IsLiveCheck,
            ResponseWriter = WriteJsonHealthReport
        });

        app.UseHealthChecks("/ready", new HealthCheckOptions
        {
            Predicate = IsReadyCheck,
            ResponseWriter = WriteJsonHealthReport
        });

        app.UseHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = IsAllCheck,
            ResponseWriter = WriteJsonHealthReport
        });

        return app;
    }

    internal static bool IsLiveCheck(HealthCheckRegistration check) => check.Tags.Contains("live");
    internal static bool IsReadyCheck(HealthCheckRegistration check) => check.Tags.Contains("ready");
    internal static bool IsAllCheck(HealthCheckRegistration _) => true;

    internal static Task WriteJsonHealthReport(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(static e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds,
                error = e.Value.Exception?.Message
            })
        };

        return context.Response.WriteAsJsonAsync(payload, cancellationToken: context.RequestAborted);
    }

    private sealed class OtlpEndpointHealthCheck(Uri endpoint) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(
                    description: $"Cannot reach OTLP collector at {endpoint.Host}:{endpoint.Port}",
                    exception: ex);
            }
        }
    }
}
