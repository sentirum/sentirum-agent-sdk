using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Tests.Fakes;

/// <summary>
/// Minimal in-memory <see cref="IChatClient"/> for tests. Returns a single
/// canned response and records the prompts it received.
/// </summary>
internal sealed class FakeChatClient : IChatClient
{
    private readonly string _cannedReply;

    public FakeChatClient(string cannedReply = "ok")
    {
        _cannedReply = cannedReply;
    }

    public List<IList<ChatMessage>> ReceivedRequests { get; } = [];

    public List<ChatOptions?> ReceivedOptions { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var list = new List<ChatMessage>(messages);
        ReceivedRequests.Add(list);
        ReceivedOptions.Add(options);

        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, _cannedReply));
        return Task.FromResult(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var list = new List<ChatMessage>(messages);
        ReceivedRequests.Add(list);
        ReceivedOptions.Add(options);

        yield return new ChatResponseUpdate(ChatRole.Assistant, _cannedReply);
        await Task.CompletedTask;
    }

    public object? GetService(System.Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
