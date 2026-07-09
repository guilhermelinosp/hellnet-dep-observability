# Hellnet Observability

Opinionated OpenTelemetry observability library for .NET 10 microservices.

## Install

```bash
dotnet add package Hellnet.Observability
```

## Required env vars

| Variable | Example |
|----------|---------|
| `HELLNET_SERVICE_NAME` | `order-api` |
| `HELLNET_OTLP_ENDPOINT` | `http://alloy.monitoring:4317` |
| `HELLNET_OTLP_PROTOCOL` | `grpc` or `http` |

## Usage

```csharp
using Hellnet.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHellnetLogging();
builder.Services.AddHellnetTracing();
builder.Services.AddHellnetMetrics();

var app = builder.Build();
app.UseHellnetObservability();
app.Run();
```

Pick only what you need:

```csharp
// Logging only
builder.Services.AddHellnetLogging();

// Tracing only
builder.Services.AddHellnetTracing();

// Metrics only
builder.Services.AddHellnetMetrics();
```

## Health checks

```csharp
builder.Services.AddHellnetHealthChecks();
app.UseHellnetHealthChecks();  // /live, /ready, /health
```

## Resilience

OTLP health check uses Polly retry (2x) + timeout (3s).  
All pipelines configurable via `HellnetResilience` static properties.

## Docs

Full reference at [docs/README.md](./docs/README.md).
