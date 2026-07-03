using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
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
        var options = LoadAndValidateOptions();
        var resourceBuilder = CreateResourceBuilder(options);
        var otlpEndpoint = new Uri(options.OtlpEndpoint);
        var logger = BuildSerilogLogger(options);
        Log.Logger = logger;
        services.AddSerilog(logger, dispose: true);
        services.ConfigureHellnetOtlpLogs(options, resourceBuilder, otlpEndpoint);
        return services;
    }

    public static IServiceCollection AddHellnetTracing(this IServiceCollection services)
    {
        var options = LoadAndValidateOptions();
        var resourceBuilder = CreateResourceBuilder(options);
        var otlpEndpoint = new Uri(options.OtlpEndpoint);
        return services.AddHellnetTracing(options, resourceBuilder, otlpEndpoint);
    }

    public static IServiceCollection AddHellnetMetrics(this IServiceCollection services)
    {
        var options = LoadAndValidateOptions();
        var resourceBuilder = CreateResourceBuilder(options);
        var otlpEndpoint = new Uri(options.OtlpEndpoint);
        return services.AddHellnetMetrics(options, resourceBuilder, otlpEndpoint);
    }

    public static IApplicationBuilder UseHellnetObservability(this IApplicationBuilder app)
    {
        var options = LoadAndValidateOptions();

        app.UseSerilogRequestLogging(loggingOptions =>
        {
            loggingOptions.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("ServiceName", options.ServiceName);
                diagnosticContext.Set("Environment", GetDotNetEnvironment());
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value);
            };
        });

        return app;
    }

    private static IServiceCollection AddHellnetTracing(this IServiceCollection services, HellnetObservabilityOptions options, ResourceBuilder resourceBuilder, Uri otlpEndpoint)
    {
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSqlClientInstrumentation()
                .AddOtlpExporter(otlp => ConfigureOtlpExporter(otlp, options, otlpEndpoint)));

        return services;
    }

    private static IServiceCollection AddHellnetMetrics(this IServiceCollection services, HellnetObservabilityOptions options, ResourceBuilder resourceBuilder, Uri otlpEndpoint)
    {
        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddOtlpExporter(otlp => ConfigureOtlpExporter(otlp, options, otlpEndpoint)));

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

    private static ResourceBuilder CreateResourceBuilder(HellnetObservabilityOptions options)
    {
        return ResourceBuilder.CreateDefault()
            .AddService(options.ServiceName)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = GetDotNetEnvironment()
            });
    }

    private static HellnetObservabilityOptions LoadOptionsFromEnvironment()
    {
        return new HellnetObservabilityOptions
        {
            ServiceName = Environment.GetEnvironmentVariable("HELLNET_SERVICE_NAME") ?? string.Empty,
            OtlpEndpoint = Environment.GetEnvironmentVariable("HELLNET_OTLP_ENDPOINT") ?? string.Empty,
            OtlpProtocol = Environment.GetEnvironmentVariable("HELLNET_OTLP_PROTOCOL") ?? string.Empty
        };
    }

    private static HellnetObservabilityOptions LoadAndValidateOptions()
    {
        var options = LoadOptionsFromEnvironment();
        ValidateOptions(options);
        return options;
    }

    private static void ValidateOptions(HellnetObservabilityOptions options)
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
    }

    private static OtlpExportProtocol ParseProtocol(string protocolString)
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

    private static string GetDotNetEnvironment()
        => Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
           ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
           ?? "Production";


    private static ILogger BuildSerilogLogger(HellnetObservabilityOptions options)
    {
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithExceptionDetails()
            .Enrich.WithEnvironmentUserName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("ServiceName", options.ServiceName)
            .Enrich.WithProperty("Environment", GetDotNetEnvironment())
            .Enrich.WithProperty("OtlpEndpoint", options.OtlpEndpoint)
            .Enrich.WithProperty("OtlpProtocol", options.OtlpProtocol);

        loggerConfiguration.WriteTo.Async(writeTo =>
            writeTo.Console(new RenderedCompactJsonFormatter()));

        return loggerConfiguration.CreateLogger();
    }
}
