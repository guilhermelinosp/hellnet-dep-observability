using Hellnet.Observability;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hellnet.Observability.Tests;

public class ObservabilityTests
{
    [Fact]
    public void AddHellnetLogging_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddHellnetLogging();
        Assert.NotNull(result);
        Assert.Same(services, result);
    }

    [Fact]
    public void AddHellnetTracing_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddHellnetTracing();
        Assert.NotNull(result);
        Assert.Same(services, result);
    }

    [Fact]
    public void AddHellnetMetrics_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddHellnetMetrics();
        Assert.NotNull(result);
        Assert.Same(services, result);
    }
}
