using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Sentirum.Agent.Tools;
using Xunit;

namespace Sentirum.Agent.Tools.Tests;

public sealed class ToolDiscoveryTests
{
    [Fact]
    public void Discover_PicksUpEveryToolDecoratedMethod()
    {
        var toolset = new SampleToolset();

        var functions = ToolDiscovery.Discover(toolset).ToList();

        functions.Should().HaveCount(2);
        functions.Select(f => f.Name).Should().BeEquivalentTo("GetOrderStatus", "calculate_tax");
    }

    [Fact]
    public void Discover_StripsAsyncSuffix_AndForwardsDescription()
    {
        var toolset = new SampleToolset();

        var function = ToolDiscovery.Discover(toolset).Single(f => f.Name == "GetOrderStatus");

        function.Description.Should().Contain("status of a Sentirum customer order");
    }

    [Fact]
    public async Task Discover_BoundDelegate_ResolvesAgainstInstance()
    {
        var toolset = new SampleToolset();
        var function = ToolDiscovery.Discover(toolset).Single(f => f.Name == "GetOrderStatus");

        var result = await function.InvokeAsync(
            new(new System.Collections.Generic.Dictionary<string, object?> { ["orderId"] = "ORD-42" }));

        result.Should().NotBeNull();
        result!.ToString().Should().Contain("ORD-42");
    }

    [Fact]
    public void WithTools_ResolvesToolsetFromDi()
    {
        var services = new ServiceCollection();
        services.AddSingleton<SampleToolset>();

        services.AddSentirumAgent("test", b => b
            .UseChatClient(_ => new FakeChatClient())
            .WithTools<SampleToolset>());

        using var sp = services.BuildServiceProvider();
        var agent = sp.GetRequiredService<ISentirumAgentRegistry>().Find("test");

        agent.Should().NotBeNull();
        // ChatOptions.Tools is set up inside SentirumAgentFactory; here we
        // just assert resolution succeeded (no MissingTools / DI exception).
    }

    private sealed class SampleToolset
    {
#pragma warning disable CA1822 // Methods on a toolset are intentionally instance members so they can take DI dependencies.
        [Tool(Description = "Look up the status of a Sentirum customer order.")]
        public Task<string> GetOrderStatusAsync(
            [Description("Order id")] string orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult($"order {orderId} OK");

        [Tool(Name = "calculate_tax", Description = "Calculate sales tax for an amount.")]
        public decimal CalculateTax(decimal amount, decimal rate) => amount * rate;

        public string NotATool() => "ignored";
#pragma warning restore CA1822
    }

    private sealed class FakeChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            System.Collections.Generic.IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        public async System.Collections.Generic.IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            System.Collections.Generic.IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, "ok");
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
