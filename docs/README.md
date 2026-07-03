# Hellnet.Observability

.NET 10 library for observability instrumentation — metrics, tracing, logging.

## Stack

- **.NET 10** — target framework
- **xUnit** — unit tests
- **Lefthook** — pre-commit hooks (format + build)

## Structure

```
.
├── app/
│   ├── src/Hellnet.Observability/      # library
│   └── tests/Hellnet.Observability.Tests/  # tests
├── docs/                                # changelog, readme, license
├── .editorconfig
├── .gitignore
├── .lefthook.yml
└── Hellnet.Observability.slnx
```

## Development

```bash
dotnet build
dotnet test
dotnet format
```
