using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Context;

/// <summary>
/// <see cref="MessageAIContextProvider"/> that runs an
/// <see cref="IKnowledgeBase"/> search keyed on the most recent user
/// message in the request and injects the top snippets as a numbered
/// block of system instructions.
/// </summary>
/// <remarks>
/// We derive from <see cref="MessageAIContextProvider"/> (not the more
/// general <see cref="AIContextProvider"/>) because
/// <see cref="MessageAIContextProvider.InvokingContext.RequestMessages"/>
/// surfaces the just-arrived user turn that we want to search on. The
/// chat-client level <see cref="AIContextProvider.InvokingContext"/> runs
/// after the inner agent appends new messages to its session and does not
/// carry the request messages directly.
/// </remarks>
public sealed class KnowledgeBaseContextProvider : MessageAIContextProvider
{
    private readonly IKnowledgeBase _knowledgeBase;
    private readonly int _maxResults;
    private readonly string _heading;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeBaseContextProvider"/> class.
    /// </summary>
    public KnowledgeBaseContextProvider(
        IKnowledgeBase knowledgeBase,
        int maxResults = 3,
        string heading = "Relevant knowledge-base entries:")
    {
        ArgumentNullException.ThrowIfNull(knowledgeBase);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxResults, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);

        _knowledgeBase = knowledgeBase;
        _maxResults = maxResults;
        _heading = heading;
    }

    /// <inheritdoc />
    protected override async ValueTask<System.Collections.Generic.IEnumerable<ChatMessage>> ProvideMessagesAsync(
        MessageAIContextProvider.InvokingContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var query = ChatMessageQueries.LatestUserText(context.RequestMessages);
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ChatMessage>();
        }

        var hits = await _knowledgeBase.SearchAsync(query, _maxResults, cancellationToken).ConfigureAwait(false);
        if (hits.Count == 0)
        {
            return Array.Empty<ChatMessage>();
        }

        var sb = new StringBuilder();
        sb.AppendLine(_heading);
        var i = 1;
        foreach (var snippet in hits)
        {
            sb.Append(i++).Append(". ").Append(snippet.Title).AppendLine();
            sb.Append("   ").AppendLine(snippet.Content);
            if (snippet.SourceUrl is { Length: > 0 } url)
            {
                sb.Append("   source: ").AppendLine(url);
            }
        }

        // Prepend a system message carrying the snippets. Returning messages
        // (instead of AIContext.Instructions) puts the content directly in
        // front of the leaf chat client, regardless of whether the provider
        // pipeline is mounted at the chat-client level or the agent level.
        return new[]
        {
            new ChatMessage(ChatRole.System, sb.ToString().TrimEnd()),
        };
    }
}
