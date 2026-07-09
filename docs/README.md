# Hellnet Observability

Opinionated OpenTelemetry observability library for .NET 10 microservices. Provides comprehensive instrumentation for distributed tracing, metrics collection, and structured logging with **mandatory environment variable configuration**.

## Quick Start (5 minutes)

### 1. Install Package
```bash
dotnet add package Hellnet.Observability
```

### 2. Set Environment Variables
```bash
export HELLNET_SERVICE_NAME=my-service
export HELLNET_OTLP_ENDPOINT=http://localhost:4317
export HELLNET_OTLP_PROTOCOL=grpc
```

### 3. Add to Program.cs
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

### 4. Use Logging
```csharp
_logger.LogInformation("Order created: {OrderId}", orderId);
```

✅ **Done!** Your service now has full observability.

---

## Features

✅ **Mandatory Environment Variables** - External configuration, fail-fast validation  
✅ **Structured Logging** - Serilog JSON output configured internally by the library  
✅ **Distributed Tracing** - OpenTelemetry with automatic instrumentation of HTTP, database, and service calls  
✅ **Metrics Collection** - CPU, memory, HTTP latency, database operations, GC statistics  
✅ **Built-in .NET Defaults** - Uses `DOTNET_ENVIRONMENT` / `ASPNETCORE_ENVIRONMENT` for environment name  
✅ **Production-Ready** - Uses async compact JSON logging with OpenTelemetry exporters  
✅ **Flexible** - Register only the pipelines you need (logging, tracing, metrics)  
✅ **Reusable** - Share across all microservices with zero code changes

---

## Extension Methods

### Add All Features
```csharp
// Adds logging (console + OTLP), tracing, and metrics
builder.Services.AddHellnetLogging();
builder.Services.AddHellnetTracing();
builder.Services.AddHellnetMetrics();
```

### Add Only Logging
```csharp
// Adds only Serilog structured logging
builder.Services.AddHellnetLogging();
```

### Add Only Tracing
```csharp
// Adds only distributed tracing
builder.Services.AddHellnetTracing();
```

### Add Only Metrics
```csharp
// Adds only metrics collection
builder.Services.AddHellnetMetrics();
```

### Mix and Match
```csharp
var builder = WebApplication.CreateBuilder(args);

// Enable only specific features
builder.Services.AddHellnetLogging();
builder.Services.AddHellnetTracing();
// Skip metrics
```

---

## Required Environment Variables

All of these **must** be set before the application starts:

| Variable | Purpose | Example |
|----------|---------|---------|
| `HELLNET_SERVICE_NAME` | Service identifier | `order-api` |
| `HELLNET_OTLP_ENDPOINT` | OTLP collector address | `http://localhost:4317` |
| `HELLNET_OTLP_PROTOCOL` | OTLP protocol | `grpc` or `http` |

**If any required variable is missing**, the application will fail at startup with:
```
InvalidOperationException: Missing required environment variables: 
HELLNET_SERVICE_NAME, HELLNET_OTLP_ENDPOINT, HELLNET_OTLP_PROTOCOL
```

### Logging Configuration

Serilog is configured internally by this library.

The library emits JSON logs via Serilog using `RenderedCompactJsonFormatter`.

---

## Optional Environment Variables

This library currently has no optional `HELLNET_*` variables.

### Protocol Options
- `grpc` - gRPC protocol (better performance, requires gRPC support)
- `http` - HTTP/Protobuf protocol (more compatible, uses more bandwidth)

---

### Logging Configuration
- **Structured JSON Output** - Compact JSON via Serilog Console sink
- **Async Console Sink** - Uses `WriteTo.Async(...)` for non-blocking console output
- **Automatic Enrichment** - Service name, environment, user, process ID, and thread ID

### Default Behavior
- **Default Level**: Information
- **Microsoft/System**: Warning overrides
- **Environment**: Controlled by `DOTNET_ENVIRONMENT` or `ASPNETCORE_ENVIRONMENT`
- **Output Format**: Compact JSON format (machine-readable)

### Example Output
```json
{"Timestamp":"2026-07-03T12:00:00.1234567Z","MessageTemplate":"Order created successfully","Level":"Information","Properties":{"ServiceName":"order-api","Environment":"Production","OrderId":456}}
{"Timestamp":"2026-07-03T12:00:01.4567890Z","MessageTemplate":"Retry attempt 1","Level":"Warning","Properties":{"ServiceName":"order-api","Environment":"Production","AttemptNumber":1}}
```


---

## What Gets Automatically Instrumented

### HTTP Requests
- All incoming HTTP requests to ASP.NET Core application
- Request method, path, and response status code
- Request latency histograms
- Automatic exception tracking

### HTTP Clients
- All outbound HttpClient requests
- Request/response details
- Latency tracking
- Error rates

### Database Operations
- Entity Framework Core queries
- SQL Server/Client operations
- Query duration and execution count
- Connection pool statistics

### System Metrics
- Process CPU and memory usage
- .NET GC statistics (collections, duration)
- Thread pool availability
- Runtime memory allocation

---

## Deployment Scenarios

### Local Development

**.env file**:
```env
HELLNET_SERVICE_NAME=order-api-dev
HELLNET_OTLP_ENDPOINT=http://localhost:4317
HELLNET_OTLP_PROTOCOL=grpc
```

**Program.cs**:
```csharp
using DotEnv.Net;

DotEnv.Load();  // Load .env file
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHellnetLogging();
builder.Services.AddHellnetTracing();
builder.Services.AddHellnetMetrics();
```

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY bin/Release/net10.0/publish .

ENV HELLNET_SERVICE_NAME=order-api
ENV HELLNET_OTLP_ENDPOINT=http://otel-collector:4317
ENV HELLNET_OTLP_PROTOCOL=grpc

ENTRYPOINT ["dotnet", "OrderApi.dll"]
```

### Kubernetes

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: observability-config
data:
  HELLNET_SERVICE_NAME: "order-api"
  HELLNET_OTLP_PROTOCOL: "grpc"
---
apiVersion: v1
kind: Secret
metadata:
  name: observability-secrets
type: Opaque
stringData:
  HELLNET_OTLP_ENDPOINT: "http://alloy.monitoring:4317"
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: order-api
spec:
  template:
    spec:
      containers:
      - name: order-api
        image: myregistry/order-api:latest
        envFrom:
        - configMapRef:
            name: observability-config
        - secretRef:
            name: observability-secrets
```

### AWS Lambda

```json
{
  "HELLNET_SERVICE_NAME": "order-function",
  "HELLNET_OTLP_ENDPOINT": "https://api.honeycomb.io/v1/traces",
  "HELLNET_OTLP_PROTOCOL": "grpc"
}
```

### Azure Functions

**local.settings.json**:
```json
{
  "Values": {
    "HELLNET_SERVICE_NAME": "order-function",
    "HELLNET_OTLP_ENDPOINT": "https://api.newrelic.com/otlp/v1/traces",
    "HELLNET_OTLP_PROTOCOL": "grpc"
  }
}
```

---

## Supported OTLP Backends

Works with any OTLP-compatible backend:

| Backend | Endpoint |
|---------|----------|
| **Grafana Cloud** | `https://otlp-gateway-<region>.grafana.net/otlp` |
| **Jaeger** | `http://jaeger-collector:4317` |
| **Honeycomb** | `https://api.honeycomb.io` |
| **New Relic** | `https://otlp.nr-data.net:443` |
| **Datadog** | Via Datadog agent |
| **SigNoz** | `http://signoz-collector:4317` |
| **Elastic Cloud** | OTLP endpoint |
| **Local OpenTelemetry Collector** | `http://localhost:4317` |

---

## Code Examples

### Web API
```csharp
[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly ILogger<OrderController> _logger;

    public OrderController(ILogger<OrderController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        _logger.LogInformation("Creating order with {@Request}", request);

        try
        {
            var order = await _orderService.CreateAsync(request);
            _logger.LogInformation("Order created: {OrderId}", order.Id);
            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            return StatusCode(500, "Internal server error");
        }
    }
}
```

### Worker Service
```csharp
var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices(services =>
{
    services.AddHostedService<OrderProcessorService>();
    services.AddHellnetLogging();
    services.AddHellnetTracing();
    services.AddHellnetMetrics();
});

var host = builder.Build();
host.UseHellnetObservability();
await host.RunAsync();
```

### gRPC Service
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGrpc();
builder.Services.AddHellnetLogging();
builder.Services.AddHellnetTracing();
builder.Services.AddHellnetMetrics();

var app = builder.Build();
app.MapGrpcService<PaymentService>();
app.UseHellnetObservability();
app.Run();
```

---

## Troubleshooting

### "Missing required environment variables"

**Cause**: One or more required variables not set

**Solution**: 
```bash
export HELLNET_SERVICE_NAME=my-service
export HELLNET_OTLP_ENDPOINT=http://localhost:4317
export HELLNET_OTLP_PROTOCOL=grpc

dotnet run
```

### "Invalid HELLNET_OTLP_ENDPOINT"

**Cause**: Endpoint is not a valid URI

**Solution**: Use valid URI format:
```bash
# ✅ Correct
HELLNET_OTLP_ENDPOINT=http://localhost:4317
HELLNET_OTLP_ENDPOINT=https://otel.example.com:4317

# ❌ Wrong
HELLNET_OTLP_ENDPOINT=localhost:4317  # Missing scheme
HELLNET_OTLP_ENDPOINT=http://localhost  # Missing port
```

### No logs appearing

**Solutions**:

1. Verify logger is injected:
   ```csharp
   // ✅ Correct
   private readonly ILogger<MyClass> _logger;
   
   // ❌ Wrong
   private readonly ILogger _logger;  // Missing generic type
   ```

2. Check log level:
   ```bash
   # Console output is async and log level is fixed by the library
   ```

### Traces not reaching collector

**Solutions**:

1. Verify OTLP endpoint is reachable:
   ```bash
   curl -i http://localhost:4317/
   ```

2. Verify collector is running:
   ```bash
   docker ps | grep otel-collector
   ```

---

## Best Practices

✅ **Service Names**: Use lowercase with hyphens  
```bash
HELLNET_SERVICE_NAME=order-api  # ✅ Good
HELLNET_SERVICE_NAME=OrderAPI   # ❌ Avoid
```

✅ **Version Control**: Commit `.env.example`, not `.env`  
```bash
# .gitignore
.env
.env.local
```

✅ **Structured Logging**: Use named properties  
```csharp
// ✅ Good
_logger.LogInformation("Order created: {OrderId}", orderId);
_logger.LogInformation("Processing {@Order}", order);

// ❌ Avoid
_logger.LogInformation("Order created: " + orderId);
```

✅ **Error Logging**: Always include exceptions  
```csharp
// ✅ Good
_logger.LogError(ex, "Error processing order {OrderId}", orderId);

// ❌ Avoid
_logger.LogError("Something went wrong");
```

✅ **Log Levels**: Use appropriate severity  
```csharp
_logger.LogDebug("Cache hit for key {Key}", key);         // Development
_logger.LogInformation("Order placed: {OrderId}", id);    // Business event
_logger.LogWarning("Retry attempt {Attempt}", count);     // Degraded condition
_logger.LogError(ex, "Database unavailable");              // Error
```

✅ **Production Configuration**: Minimal logging  
```env
HELLNET_SERVICE_NAME=order-api
HELLNET_OTLP_ENDPOINT=https://otel-collector.example.com:4317
HELLNET_OTLP_PROTOCOL=grpc
```

---

## Technology Stack

- **.NET 10.0** - Target framework
- **OpenTelemetry 1.16.0** - Tracing, metrics, logs
- **Serilog 4.3.0** - Structured logging with JSON output
- **Microsoft.Extensions.DependencyInjection** - Dependency injection
- **OpenTelemetry Instrumentation Libraries**:
  - ASP.NET Core
  - Entity Framework Core
  - HTTP Client
  - SQL Client
  - Process (CPU, memory)
  - Runtime (.NET GC, threads)

---

## Installation

### Via NuGet
```bash
dotnet add package Hellnet.Observability
```

### Via Package Manager
```
Install-Package Hellnet.Observability
```

### Manual
Add to `.csproj`:
```xml
<ItemGroup>
    <PackageReference Include="Hellnet.Observability" Version="1.0.0-beta.1" />
</ItemGroup>
```

---

## Architecture Overview

```
Your Application
      ↓
AddHellnetLogging()
AddHellnetTracing()
AddHellnetMetrics()
      ↓
   ┌──┴──────────────────────┐
   │                          │
   ↓                          ↓
Serilog                   OpenTelemetry
(Logging - JSON)             ├─ Tracing
   │                         ├─ Metrics
   │                         └─ Logs
   │                          │
   ├─────────────────┬────────┘
   │                 │
   ↓                 ↓
Console          OTLP Exporter
Output           (gRPC/HTTP)
                    ↓
             OTLP Collector
             (Jaeger, Alloy,
              Honeycomb, etc)
```

---

## GitHub

- **Repository**: https://github.com/guilhermelinosp/hellnet-dep-observability
- **Issues**: https://github.com/guilhermelinosp/hellnet-dep-observability/issues
- **NuGet**: https://www.nuget.org/packages/Hellnet.Observability/

## License

MIT

---

## Version

- **Current**: 1.0.0-beta.1
- **.NET**: 10.0
- **OpenTelemetry**: 1.16.0
- **Serilog**: 4.3.0


## Features

✅ **Structured Logging** - Serilog with automatic enrichment (machine name, process ID, thread ID, service name)  
✅ **Distributed Tracing** - OpenTelemetry with automatic instrumentation for HTTP, database, and service calls  
✅ **Metrics Collection** - Runtime metrics, process metrics, HTTP metrics, and database performance  
✅ **Fail-Fast Validation** - Required environment variables are validated at startup  
✅ **Environment-Driven Configuration** - All configuration via environment variables, no hardcoded values  
✅ **Production-Ready** - Optimized for high-throughput, async logging by default  
✅ **Auto-Instrumentation** - Automatically instruments ASP.NET Core, HttpClient, Entity Framework, SQL Server

## Quick Start

### 1. Install Package

```bash
dotnet add package Hellnet.Observability
```

### 2. Set Environment Variables

```bash
export HELLNET_SERVICE_NAME=my-service
export HELLNET_OTLP_ENDPOINT=http://localhost:4317
export HELLNET_OTLP_PROTOCOL=grpc
```

### 3. Add to Program.cs

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

### 4. Use Logging

```csharp
_logger.LogInformation("Order created: {OrderId}", orderId);
```

Done! Your service now has:
- 📊 Structured logging with context
- 📈 Metrics and traces sent to OTLP collector
- 🔍 Automatic instrumentation of HTTP, database operations
- ⚠️ Fail-fast validation of configuration

## Required Environment Variables

These are the only environment variables used by the library:

| Variable | Purpose | Example |
|----------|---------|---------|
| `HELLNET_SERVICE_NAME` | Service identifier | `order-api` |
| `HELLNET_OTLP_ENDPOINT` | OTLP collector address | `http://localhost:4317` |
| `HELLNET_OTLP_PROTOCOL` | OTLP transport protocol | `grpc` or `http` |

`HELLNET_SERVICE_NAME` and `HELLNET_OTLP_ENDPOINT` are required. `HELLNET_OTLP_PROTOCOL` is optional and defaults to `string.Empty`, which makes the exporter keep its own default protocol behavior.

If a required variable is missing, the application will fail immediately with a clear error:

```
InvalidOperationException: Missing required environment variables: 
HELLNET_SERVICE_NAME, HELLNET_OTLP_ENDPOINT
```

## Optional Environment Variables

There are no feature-toggle environment variables for console/tracing/metrics/logs. Console logging uses `Async` by default.

## Installation

Install via NuGet:

```bash
dotnet add package Hellnet.Observability
```

Or via Package Manager:

```
Install-Package Hellnet.Observability
```

**NuGet Package**: https://www.nuget.org/packages/Hellnet.Observability

## Documentation

- **[IMPLEMENTATION_GUIDE.md](../IMPLEMENTATION_GUIDE.md)** - Step-by-step guide to add to your microservice
- **[ENV_REFERENCE.md](../ENV_REFERENCE.md)** - Complete environment variable reference
- **[USAGE.md](../USAGE.md)** - Advanced usage patterns and deployment scenarios
- **[EXAMPLES.md](../EXAMPLES.md)** - Full code examples (ASP.NET Core, Worker Service, gRPC, Docker, Kubernetes)
- **[.env.example](../.env.example)** - Template environment variables file

## What Gets Instrumented

### HTTP Requests
- ASP.NET Core HTTP middleware
- All request/response attributes (method, path, status code)
- Request duration and throughput
- Automatic exception tracking

### Database Operations
- Entity Framework Core queries
- SQL Server/Client queries
- Query duration and execution count
- Connection pool statistics

### Outbound HTTP Calls
- HttpClient requests
- Request/response headers
- Latency and error rates
- Automatic timeout tracking

### Application Metrics
- Process memory and CPU usage
- .NET GC statistics (collections, duration)
- Thread pool statistics
- HTTP request metrics (latency histograms, throughput)

### Logs
- Structured log entries with context
- Automatic enrichment (service name, environment, machine name)
- Exception details and stack traces
- OpenTelemetry log export

## Project Structure

```
.
├── app/
│   ├── src/Hellnet.Observability/        # Main library
│   └── tests/Hellnet.Observability.Tests/ # Unit tests
├── docs/                                   # README, changelog, license
├── bin/packages/                           # NuGet packages
├── .env.example                            # Environment variables template
├── IMPLEMENTATION_GUIDE.md                 # How to use in microservices
├── ENV_REFERENCE.md                        # Environment variable docs
├── USAGE.md                                # Advanced usage
├── EXAMPLES.md                             # Code examples
└── Hellnet.Observability.slnx              # Solution file
```

## Technology Stack

- **.NET 10** - Target framework
- **OpenTelemetry 1.16.0** - Tracing, metrics, logs
- **Serilog 4.3.0** - Structured logging
- **Microsoft.Extensions.DependencyInjection** - Dependency injection

## Architecture

```
Application
    ↓
IServiceCollection.AddHellnetLogging()
IServiceCollection.AddHellnetTracing()
IServiceCollection.AddHellnetMetrics()
    ├─→ Validates environment variables
    ├─→ Configures Serilog for structured logging
    ├─→ Sets up OpenTelemetry tracing
    ├─→ Enables metrics collection
    └─→ Configures OTLP export
    ↓
Application Logs/Traces/Metrics
    ↓
OTLP Collector
    ├─→ Grafana Alloy
    ├─→ Jaeger
    ├─→ Honeycomb
    └─→ New Relic
```

## Environment Configuration Examples

### Local Development

```env
HELLNET_SERVICE_NAME=order-api-dev
HELLNET_OTLP_ENDPOINT=http://localhost:4317
HELLNET_OTLP_PROTOCOL=grpc
```

### Docker/Container

```env
HELLNET_SERVICE_NAME=order-api
HELLNET_OTLP_ENDPOINT=http://otel-collector:4317
HELLNET_OTLP_PROTOCOL=http
```

### Kubernetes

```yaml
env:
- name: HELLNET_SERVICE_NAME
  value: "order-api"
- name: HELLNET_OTLP_ENDPOINT
  value: "http://alloy.monitoring:4317"
- name: HELLNET_OTLP_PROTOCOL
  value: "grpc"
```

## Development

```bash
# Build
dotnet build

# Build Release
dotnet build -c Release

# Test
dotnet test

# Format
dotnet format

# Package
dotnet pack -c Release
```

## Publishing

To publish a new version to NuGet, see [PUBLISH.md](PUBLISH.md).

## Error Handling

### Missing Required Variables

```
InvalidOperationException: Missing required environment variables: 
HELLNET_SERVICE_NAME, HELLNET_OTLP_ENDPOINT

Ensure all required environment variables are set before running the application.
```

**Solution**: Set the required environment variables (`HELLNET_SERVICE_NAME` and `HELLNET_OTLP_ENDPOINT`) before starting the application.

### Invalid OTLP Endpoint

```
InvalidOperationException: Invalid HELLNET_OTLP_ENDPOINT. 
Must be a valid absolute URI. Got: localhost:4317
```

**Solution**: Use a valid URI: `http://localhost:4317` or `https://collector.example.com:4317`

## Best Practices

1. **Use `.env.example`** - Commit this file to version control
2. **Never commit `.env`** - Add to `.gitignore` for local secrets
3. **Use structured logging** - Log with named properties, not string concatenation
4. **Include exceptions** - Always pass the exception to LogError/LogCritical
5. **Meaningful service names** - Use lowercase with hyphens: `order-api`, not `OrderAPI`
6. **Appropriate log levels** - Debug for dev, Information for staging, Warning for production
7. **Async logging by default** - Console output already uses `Async` in the library

## Supported OTLP Backends

This library works with any OTLP-compatible backend:

- **Grafana Cloud** - `https://otlp-gateway-<region>.grafana.net/otlp`
- **Jaeger** - `http://jaeger-collector:4317`
- **Honeycomb** - `https://api.honeycomb.io`
- **New Relic** - `https://otlp.nr-data.net:443`
- **Datadog** - OTLP support via Datadog agent
- **SigNoz** - `http://signoz-otel-collector:4317`

See [EXAMPLES.md](../EXAMPLES.md) for integration examples.

## License

MIT

## Support

- **GitHub Issues**: https://github.com/guilhermelinosp/hellnet-dep-observability/issues
- **NuGet Package**: https://www.nuget.org/packages/Hellnet.Observability
- **Documentation**: See `.md` files in repository root
