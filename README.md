# Hellnet Observability

Library de observabilidade para microsserviços .NET 10, baseada em OpenTelemetry com structured logging (Serilog), distributed tracing e metrics — setup em 1 linha, native OTel API.

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

### All-in-one (recomendado)

```csharp
builder.Services.AddHellnetTelemetry();
```

Registra logging, tracing, metrics, health checks e `ITelemetry` em 1 chamada.

### Pipelines individuais

```csharp
builder.Services.AddHellnetLogging();           // Serilog + OTLP
builder.Services.AddHellnetTracing(t => ...);   // OTel tracing
builder.Services.AddHellnetMetrics(m => ...);   // OTel metrics
builder.Services.AddHellnetHealthChecks();      // /live, /ready, /health
```

### Injetar telemetry em qualquer lugar

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
// Endpoints (depois de UseHellnetHealthChecks):
//   GET /live    — liveness probe (self check)
//   GET /ready   — readiness probe (self + OTLP collector)
//   GET /health  — aggregate

// Adicionar custom checks:
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

### Resiliência (Polly)

Health check do OTLP collector usa retry (2x) + timeout (3s).  
Configurável via `HellnetResilience`:

```csharp
HellnetResilience.RetryCount = 5;
HellnetResilience.TimeoutDuration = TimeSpan.FromSeconds(10);
```

## Required env vars

| Variable | Exemplo | Descrição |
|----------|---------|-----------|
| `HELLNET_SERVICE_NAME` | `order-api` | Service identifier |
| `HELLNET_OTLP_ENDPOINT` | `http://alloy.monitoring:4317` | OTLP collector endpoint |
| `HELLNET_OTLP_PROTOCOL` | `grpc` / `http` | Transport protocol |

Fail-fast: a aplicação não inicia sem essas três.

| Variable | Default | Descrição |
|----------|---------|-----------|
| `HELLNET_LOG_LEVEL` | `Information` | Mínimo level de log |
| `HELLNET_ENV_FILE` | — | Caminho do `.env` em development |

## Instrumentação automática

| Camada | Package |
|--------|---------|
| HTTP (incoming) | `OpenTelemetry.Instrumentation.AspNetCore` |
| HTTP (outgoing) | `OpenTelemetry.Instrumentation.Http` |
| Database | `OpenTelemetry.Instrumentation.EntityFrameworkCore` |
| SQL | `OpenTelemetry.Instrumentation.SqlClient` |
| Runtime | `OpenTelemetry.Instrumentation.Runtime` |
| Process | `OpenTelemetry.Instrumentation.Process` |

Inclusas por padrão no `AddHellnetTelemetry()`.

## Tech stack

| Categoria | Library |
|-----------|---------|
| **OTel** | OpenTelemetry SDK 1.16.0 + OTLP exporter |
| **Logging** | Serilog 4.3.1 (compact JSON, async console) |
| **Resilience** | Polly.Core 8.7.0 |
| **.NET** | 10.0, C# 14 |

## Licença

Apache 2.0 © 2026 Hellnet
