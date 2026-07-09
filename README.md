# Hellnet Observability

Opinionated OpenTelemetry observability library for .NET 10 microservices. Structured logging, distributed tracing, and metrics — one-liner setup, native OTel API.

```
dotnet add package Hellnet.Observability
```

---

## Quick start

```bash
export HELLNET_SERVICE_NAME=my-service
export HELLNET_OTLP_ENDPOINT=http://alloy.monitoring:4317
export HELLNET_OTLP_PROTOCOL=grpc
```

```csharp
using Hellnet.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHellnetTelemetry();

var app = builder.Build();
app.UseHellnetObservability();
app.UseHellnetHealthChecks();
app.Run();
```

## Usage

### All-in-one (recommended)

```csharp
builder.Services.AddHellnetTelemetry();
```

Registers logging, tracing, metrics, health checks, and `ITelemetry` in one call.

### Individual pipelines

```csharp
builder.Services.AddHellnetLogging();           // Serilog + OTLP
builder.Services.AddHellnetTracing(t => ...);   // OTel tracing
builder.Services.AddHellnetMetrics(m => ...);   // OTel metrics
builder.Services.AddHellnetHealthChecks();      // /live, /ready, /health
```

### Inject telemetry anywhere

```csharp
public class OrderService(ITelemetry tel)
{
    public async Task Process(Order order)
    {
        using var span = tel.ActivitySource.StartActivity("process-order");
        span?.SetTag("order.id", order.Id);

        tel.Logger<OrderService>().LogInformation("Processing {Id}", order.Id);

        tel.Meter.CreateCounter<long>("orders.processed").Add(1);
        tel.Meter.CreateHistogram<double>("order.value").Record((double)order.Total);
    }
}
```

### Health checks

```csharp
// Map endpoints (after UseHellnetHealthChecks)
//   GET /live    — liveness probe (self check)
//   GET /ready   — readiness probe (self + OTLP collector)
//   GET /health  — aggregate

// Optional: add custom checks
builder.Services.AddHellnetHealthChecks(checks =>
    checks.AddCheck("db", new MyDbCheck(), tags: ["ready"]));
```

### Custom tracing source

```csharp
builder.Services.AddHellnetTracing(t =>
    t.AddSource("MyApp.CustomTraces"));
```

### Custom meter

```csharp
builder.Services.AddHellnetMetrics(m =>
    m.AddMeter("MyApp.CustomMetrics"));
```

### Resilience (Polly)

Health check do OTLP collector usa retry (2x) + timeout (3s).  
Configurável via `HellnetResilience`:

```csharp
HellnetResilience.RetryCount = 5;
HellnetResilience.TimeoutDuration = TimeSpan.FromSeconds(10);
```

## Required env vars

| Variable | Example | Description |
|----------|---------|-------------|
| `HELLNET_SERVICE_NAME` | `order-api` | Service identifier |
| `HELLNET_OTLP_ENDPOINT` | `http://alloy.monitoring:4317` | OTLP collector endpoint |
| `HELLNET_OTLP_PROTOCOL` | `grpc` / `http` | OTLP transport protocol |

Fail-fast: app não inicia sem essas três. As demais são opcionais:

| Variable | Default | Description |
|----------|---------|-------------|
| `HELLNET_LOG_LEVEL` | `Information` | Minimum log level |
| `HELLNET_ENV_FILE` | — | Path to `.env` in development |

## Instrumentação automática

| Camada | Pacote |
|--------|--------|
| HTTP (incoming) | `OpenTelemetry.Instrumentation.AspNetCore` |
| HTTP (outgoing) | `OpenTelemetry.Instrumentation.Http` |
| Database | `OpenTelemetry.Instrumentation.EntityFrameworkCore` |
| SQL | `OpenTelemetry.Instrumentation.SqlClient` |
| Runtime | `OpenTelemetry.Instrumentation.Runtime` |
| Process | `OpenTelemetry.Instrumentation.Process` |

Inclusas por padrão no `AddHellnetTelemetry()` e `AddHellnetTracing/Metrics()`.

## Tech stack

| Category | Library |
|----------|---------|
| **OTel** | OpenTelemetry SDK 1.16.0 + OTLP exporter |
| **Logging** | Serilog 4.3.1 (compact JSON, async console) |
| **Resilience** | Polly.Core 8.7.0 |
| **.NET** | 10.0, C# 14 |
| **Tests** | xUnit, FluentAssertions, Moq, AutoFixture, coverlet (94.8%) |

## ADR — Architecture Decision Records

Decisões técnicas documentadas em:

[https://gist.github.com/guilhermelinosp/27c941772c6c4184d41875d8de15ab84](https://gist.github.com/guilhermelinosp/27c941772c6c4184d41875d8de15ab84)

Topics covered: OTel as single framework, 12-factor config, three pipelines, health checks, Polly resilience, ITelemetry abstraction, auto-instrumentation.

## License

Apache 2.0 — © 2026 Hellnet
