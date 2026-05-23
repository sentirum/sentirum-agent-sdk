using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Workflows.Tests.Fakes;

/// <summary>
/// Minimal <see cref="AIAgent"/> implementation for workflow tests. Each
/// run echoes the joined transcript with a configurable suffix so a
/// sequential pipeline produces a deterministic, easy-to-assert string.
/// </summary>
internal sealed class EchoAgent : AIAgent
{
    private readonly string _id;
    private readonly string _suffix;

    public EchoAgent(string id, string suffix)
    {
        _id = id;
        _suffix = suffix;
    }

    protected override string IdCore => _id;

    public override string Name => _id;

    public int Invocations { get; private set; }

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken)
        => new(new EmptyAgentSession());

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions,
        CancellationToken cancellationToken)
        => new(new EmptyAgentSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions,
        CancellationToken cancellationToken)
        => new(JsonDocument.Parse("{}").RootElement);

    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        CancellationToken cancellationToken)
    {
        Invocations++;
        var transcript = string.Join(" | ", messages.Select(m => m.Text));
        var reply = string.IsNullOrEmpty(transcript) ? _suffix : $"{transcript} {_suffix}";
        var response = new AgentResponse(new ChatMessage(ChatRole.Assistant, reply))
        {
            AgentId = Id,
        };
        return Task.FromResult(response);
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Invocations++;
        var transcript = string.Join(" | ", messages.Select(m => m.Text));
        var reply = string.IsNullOrEmpty(transcript) ? _suffix : $"{transcript} {_suffix}";
        yield return new AgentResponseUpdate(ChatRole.Assistant, reply) { AgentId = Id };
        await Task.CompletedTask;
    }

    private sealed class EmptyAgentSession : AgentSession
    {
    }
}
