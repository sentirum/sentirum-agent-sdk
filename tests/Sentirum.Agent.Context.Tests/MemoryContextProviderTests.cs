using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Sentirum.Agent.Memory;
using Sentirum.Agent.Tests.Fakes;
using Xunit;

namespace Sentirum.Agent.Context.Tests;

public class MemoryContextProviderTests
{
    [Fact]
    public async Task WithUserMemory_InjectsStoredFactsIntoEverySystemPrompt()
    {
        var services = new ServiceCollection();
        services.AddSentirumInMemoryMemory();

        var fake = new FakeChatClient("ok");
        services.AddSentirumAgent("a", b => b
            .UseChatClient(_ => fake)
            .WithInstructions("You are helpful.")
            .WithUserMemory(userId: "u-1"));

        using var sp = services.BuildServiceProvider();
        var memory = sp.GetRequiredService<ISentirumMemoryStore>();
        await memory.SetAsync(MemoryPartition.ForUser("u-1"), "name", "Ersin");
        await memory.SetAsync(MemoryPartition.ForUser("u-1"), "city", "Istanbul");

        var agent = sp.GetRequiredService<ISentirumAgentRegistry>().Find("a")!;
        var session = await sp.GetRequiredService<ISentirumSessionStore>().CreateAsync(agent.Id);

        await agent.RunAsync(session, new ChatMessage(ChatRole.User, "hello"));

        // The injected memory text shows up via either a system message
        // or ChatOptions.Instructions depending on MAF's pipeline. Inspect
        // both surfaces.
        var msgText = string.Join("\n", fake.ReceivedRequests.Last().Select(m => m.Text));
        var optsText = fake.ReceivedOptions.Last()?.Instructions ?? string.Empty;
        var combined = msgText + "\n" + optsText;

        combined.Should().Contain("name: Ersin");
        combined.Should().Contain("city: Istanbul");
    }

    [Fact]
    public async Task EmptyPartition_DoesNotPolluteSystemPrompt()
    {
        var services = new ServiceCollection();
        services.AddSentirumInMemoryMemory();

        var fake = new FakeChatClient("ok");
        services.AddSentirumAgent("a", b => b
            .UseChatClient(_ => fake)
            .WithInstructions("You are helpful.")
            .WithUserMemory(userId: "u-1", heading: "Known facts:"));

        using var sp = services.BuildServiceProvider();
        var agent = sp.GetRequiredService<ISentirumAgentRegistry>().Find("a")!;
        var session = await sp.GetRequiredService<ISentirumSessionStore>().CreateAsync(agent.Id);

        await agent.RunAsync(session, new ChatMessage(ChatRole.User, "hi"));

        var msgText = string.Join("\n", fake.ReceivedRequests.Last().Select(m => m.Text));
        var optsText = fake.ReceivedOptions.Last()?.Instructions ?? string.Empty;
        (msgText + "\n" + optsText).Should().NotContain("Known facts:");
    }
}
