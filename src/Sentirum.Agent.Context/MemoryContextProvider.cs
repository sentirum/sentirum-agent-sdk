using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sentirum.Agent.Memory;

namespace Sentirum.Agent.Context;

/// <summary>
/// <see cref="AIContextProvider"/> that injects every entry from a
/// configured <see cref="MemoryPartition"/> into the agent's instructions
/// as a bulleted block.
/// </summary>
/// <remarks>
/// <para>
/// The provider resolves the partition per-call from a factory so it can
/// derive identifiers from <see cref="AIContextProvider.InvokingContext"/>
/// (typically the session id or a user id stashed in
/// <see cref="AgentRunContext"/>). When the partition is empty the
/// provider contributes nothing.
/// </para>
/// <para>
/// For larger payloads pair with a semantic search context provider so
/// the injected block stays bounded.
/// </para>
/// </remarks>
public sealed class MemoryContextProvider : MessageAIContextProvider
{
    private readonly ISentirumMemoryStore _store;
    private readonly Func<MessageAIContextProvider.InvokingContext, MemoryPartition> _partitionFactory;
    private readonly string _heading;
    private readonly int _maxEntries;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryContextProvider"/> class.
    /// </summary>
    /// <param name="store">Backing memory store.</param>
    /// <param name="partitionFactory">Resolves the partition to read per request.</param>
    /// <param name="heading">Heading printed before the injected entries.</param>
    /// <param name="maxEntries">Maximum entries to inject; protects context length.</param>
    public MemoryContextProvider(
        ISentirumMemoryStore store,
        Func<MessageAIContextProvider.InvokingContext, MemoryPartition> partitionFactory,
        string heading = "Known facts about the user:",
        int maxEntries = 20)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(partitionFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEntries, 1);

        _store = store;
        _partitionFactory = partitionFactory;
        _heading = heading;
        _maxEntries = maxEntries;
    }

    /// <inheritdoc />
    protected override async ValueTask<System.Collections.Generic.IEnumerable<ChatMessage>> ProvideMessagesAsync(
        MessageAIContextProvider.InvokingContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var partition = _partitionFactory(context);

        var entries = new List<MemoryEntry>(_maxEntries);
        await foreach (var entry in _store.ListAsync(partition, cancellationToken).ConfigureAwait(false))
        {
            entries.Add(entry);
            if (entries.Count >= _maxEntries)
            {
                break;
            }
        }

        if (entries.Count == 0)
        {
            return Array.Empty<ChatMessage>();
        }

        var sb = new StringBuilder();
        sb.AppendLine(_heading);
        foreach (var entry in entries)
        {
            sb.Append("- ").Append(entry.Key).Append(": ").AppendLine(entry.Value);
        }

        // Returning messages (instead of AIContext.Instructions) ensures
        // the data lands in front of the leaf chat client even when the
        // provider pipeline is mounted at the agent level.
        return new[]
        {
            new ChatMessage(ChatRole.System, sb.ToString().TrimEnd()),
        };
    }
}
