using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Sentirum.Agent.Providers.MiniMax.Tests;

public sealed class MiniMaxBuilderTests
{
    [Fact]
    public void UseMiniMax_OpenAI_RegistersAgent()
    {
        var services = new ServiceCollection();
        services.AddSentirumAgent("minimax-openai", b => b
            .UseMiniMax("MiniMax-M2.7", apiKey: "test-key", protocol: MiniMaxProtocol.OpenAI));

        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<ISentirumAgentRegistry>();

        Assert.NotNull(registry.Find("minimax-openai"));
    }

    [Fact]
    public void UseMiniMax_Anthropic_RegistersAgent()
    {
        var services = new ServiceCollection();
        services.AddSentirumAgent("minimax-anthropic", b => b
            .UseMiniMax("MiniMax-M2.7", apiKey: "test-key", protocol: MiniMaxProtocol.Anthropic));

        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<ISentirumAgentRegistry>();

        Assert.NotNull(registry.Find("minimax-anthropic"));
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
    public void UseMiniMax_DefaultProtocol_IsOpenAI()
    {
        var services = new ServiceCollection();
        services.AddSentirumAgent("minimax-default", b => b
            .UseMiniMax("MiniMax-M2.7", apiKey: "test-key"));

        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<ISentirumAgentRegistry>();

        Assert.NotNull(registry.Find("minimax-default"));
    }
}
