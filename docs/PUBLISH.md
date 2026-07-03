# Publishing to NuGet

This document describes how to publish `Hellnet.Observability` to NuGet.

## Prerequisites

1. **NuGet API Key** — obtain from https://www.nuget.org/account/apikeys
2. **.NET SDK** — version 10.0 or later

## Building the Package

Generate the NuGet package in Release mode:

```bash
dotnet pack app/src/Hellnet.Observability/Hellnet.Observability.csproj -c Release -o ./bin/packages
```

This creates `Hellnet.Observability.X.Y.Z.nupkg` in `./bin/packages`.

## Publishing to NuGet

### Option 1: Using `dotnet nuget push` (recommended)

```bash
dotnet nuget push ./bin/packages/Hellnet.Observability.1.0.0.nupkg \
  --api-key <YOUR_API_KEY> \
  --source https://api.nuget.org/v3/index.json
```

### Option 2: Using `nuget push` (legacy tool)

```bash
nuget push ./bin/packages/Hellnet.Observability.1.0.0.nupkg \
  <YOUR_API_KEY> \
  -Source https://api.nuget.org/v3/index.json
```

## Verifying the Package

After publishing, verify the package on NuGet:

- **Package URL**: https://www.nuget.org/packages/Hellnet.Observability
- **Search**: https://www.nuget.org/packages?q=Hellnet.Observability

## Updating the Version

Before publishing a new release, update the version in:

```xml
<!-- app/src/Hellnet.Observability/Hellnet.Observability.csproj -->
<Version>X.Y.Z</Version>
```

Then rebuild and push the new package.

## Automatic Publishing (CI/CD)

To automate publishing via GitHub Actions, add this workflow:

```yaml
# .github/workflows/publish-nuget.yml
name: Publish to NuGet

on:
  push:
    tags:
      - 'v*'

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      - run: dotnet pack app/src/Hellnet.Observability/Hellnet.Observability.csproj -c Release -o ./bin/packages
      - run: dotnet nuget push ./bin/packages/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
```

Then push a tag to trigger the workflow:

```bash
git tag v1.0.0
git push origin v1.0.0
```

## Package Metadata

Current package configuration (in `.csproj`):

- **Id**: `Hellnet.Observability`
- **Version**: `1.0.0`
- **Description**: .NET 10 library for observability instrumentation — metrics, tracing, logging.
- **Authors**: Hellnet
- **License**: MIT
- **Repository**: https://github.com/guilhermelino/hellnet-dep-observability

Update these values in the `.csproj` file as needed before publishing.

