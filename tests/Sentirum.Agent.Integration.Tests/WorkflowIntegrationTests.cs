using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Sentirum.Agent;
using Sentirum.Agent.Testing;
using Sentirum.Agent.Workflows;
using Xunit;

namespace Sentirum.Agent.Integration.Tests;

/// <summary>
/// End-to-end workflow integration tests using replay fixtures.
/// </summary>
public sealed class WorkflowIntegrationTests
{
    private static IEnumerable<string> ExtractTexts(IReadOnlyList<object?> outputs)
    {
        foreach (var output in outputs)
        {
            if (output is string s)
            {
                yield return s;
            }
            else if (output is ChatMessage msg)
            {
                yield return msg.Text ?? string.Empty;
            }
            else if (output is IEnumerable<ChatMessage> msgs)
            {
                foreach (var m in msgs)
                {
                    yield return m.Text ?? string.Empty;
                }
            }
            else if (output is not null)
            {
                yield return output.ToString() ?? string.Empty;
            }
        }
    }

    [Fact]
    public async Task SequentialWorkflow_RunsStepsInOrder()
    {
        var json = await File.ReadAllTextAsync(Path.Combine("Fixtures", "workflow-sequential.json"));

        var services = new ServiceCollection()
            .AddSentirumCore();

        var replay = ReplayChatClient.LoadFromString(json, ReplayChatClient.MatchStrategy.Sequential);
        services.AddSentirumAgent("step1", b => b.UseChatClient(_ => replay));
        services.AddSentirumAgent("step2", b => b.UseChatClient(_ => replay));

        var sp = services.BuildServiceProvider();
        var agent1 = sp.GetAgent("step1");
        var agent2 = sp.GetAgent("step2");

        var workflow = SentirumWorkflowBuilder.Create("test-seq")
            .Sequential([agent1, agent2])
            .Build();

        var result = await workflow.RunAsync(
            new List<ChatMessage> { new(ChatRole.User, "start") },
            sessionId: Guid.NewGuid().ToString("n"));

        var texts = ExtractTexts(result.Outputs).ToList();
        texts.Should().Contain("Hello!");
        texts.Should().Contain("What is your name?");
    }

    [Fact]
    public async Task ConcurrentJoinWorkflow_AggregatesOutputs()
    {
        var json = await File.ReadAllTextAsync(Path.Combine("Fixtures", "workflow-concurrent.json"));

        var services = new ServiceCollection()
            .AddSentirumCore();

        var replay = ReplayChatClient.LoadFromString(json, ReplayChatClient.MatchStrategy.Sequential);
        services.AddSentirumAgent("agent-a", b => b.UseChatClient(_ => replay));
        services.AddSentirumAgent("agent-b", b => b.UseChatClient(_ => replay));

        var sp = services.BuildServiceProvider();
        var agentA = sp.GetAgent("agent-a");
        var agentB = sp.GetAgent("agent-b");

        var workflow = SentirumWorkflowBuilder.Create("test-concurrent")
            .ConcurrentJoin([agentA, agentB])
            .Build();

        var result = await workflow.RunAsync(
            new List<ChatMessage> { new(ChatRole.User, "analyze") },
            sessionId: Guid.NewGuid().ToString("n"));

        result.Outputs.Should().NotBeEmpty();
        var texts = ExtractTexts(result.Outputs).ToList();
        texts.Should().Contain("Positive sentiment detected.");
        texts.Should().Contain("Keywords: sentiment, analysis.");
    }

    [Fact]
    public async Task HandoffWorkflow_DelegatesToSpecialist()
    {
        var json = await File.ReadAllTextAsync(Path.Combine("Fixtures", "handoff-triage.json"));

        var services = new ServiceCollection()
            .AddSentirumCore();

        var replay = ReplayChatClient.LoadFromString(json, ReplayChatClient.MatchStrategy.Sequential);
        services.AddSentirumAgent("triage", b => b.UseChatClient(_ => replay));
        services.AddSentirumAgent("billing", b => b.UseChatClient(_ => replay));

        var sp = services.BuildServiceProvider();
        var triage = sp.GetAgent("triage");
        var billing = sp.GetAgent("billing");

        var workflow = SentirumWorkflowBuilder.Create("test-handoff")
            .Handoff(triage, [billing])
            .Build();

        var result = await workflow.RunAsync(
            new List<ChatMessage> { new(ChatRole.User, "billing issue") },
            sessionId: Guid.NewGuid().ToString("n"));

        var texts = ExtractTexts(result.Outputs).ToList();
        texts.Should().Contain(s => s.Contains("billing"));
    }
}
