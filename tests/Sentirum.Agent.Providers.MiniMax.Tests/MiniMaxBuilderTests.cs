using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Sentirum.Agent.Providers.MiniMax.Tests;

public sealed class MiniMaxBuilderTests
{
    [Fact]
    public void UseMiniMax_RegistersAgent()
    {
        var services = new ServiceCollection();
        services.AddSentirumAgent("mm", b => b
            .UseMiniMax("MiniMax-M2.7", apiKey: "test-key"));

        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<ISentirumAgentRegistry>();

        Assert.NotNull(registry.Find("mm"));
    }

    [Fact]
    public void UseMiniMax_WithCustomBaseUrl_RegistersAgent()
    {
        var services = new ServiceCollection();
        services.AddSentirumAgent("mm-custom", b => b
            .UseMiniMax("MiniMax-M2.7", apiKey: "test-key",
                baseUrl: "https://custom.api.io/v1/"));

        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<ISentirumAgentRegistry>();

        Assert.NotNull(registry.Find("mm-custom"));
    }

    [Fact]
    public void UseMiniMax_WithoutReasoningSplit_RegistersAgent()
    {
        var services = new ServiceCollection();
        services.AddSentirumAgent("mm-nors", b => b
            .UseMiniMax("MiniMax-M2.7", apiKey: "test-key",
                reasoningSplit: false));

        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<ISentirumAgentRegistry>();

        Assert.NotNull(registry.Find("mm-nors"));
    }

    [Theory]
    [InlineData(null, "key")]
    [InlineData("model", null)]
    [InlineData("", "key")]
    [InlineData("model", "")]
    public void UseMiniMax_InvalidArguments_Throws(string? model, string? key)
    {
        var services = new ServiceCollection();

        Assert.ThrowsAny<ArgumentException>(() =>
            services.AddSentirumAgent("bad", b => b
                .UseMiniMax(model!, key!)));
    }

    [Fact]
    public void UseMiniMax_DefaultModel_Works()
    {
        var services = new ServiceCollection();
        services.AddSentirumAgent("mm-default", b => b
            .UseMiniMax("MiniMax-M2.7-highspeed", apiKey: "test-key"));

        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<ISentirumAgentRegistry>();

        Assert.NotNull(registry.Find("mm-default"));
    }
}
