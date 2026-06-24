using System.Collections.Generic;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Context;

/// <summary>
/// Small, reusable queries over a context provider's request message
/// list. Shared by context providers that need the latest user turn
/// (knowledge-base grounding, sentiment escalation, …).
/// </summary>
public static class ChatMessageQueries
{
    /// <summary>
    /// Returns the text of the most recent <see cref="ChatRole.User"/>
    /// message in <paramref name="messages"/>, or <see langword="null"/>
    /// when there is none.
    /// </summary>
    /// <remarks>
    /// Iterates to the end so the <em>last</em> user turn wins even when the
    /// request carries several. Returns the first non-empty user message's
    /// <see cref="ChatMessage.Text"/>.
    /// </remarks>
    public static string? LatestUserText(IEnumerable<ChatMessage>? messages)
    {
        if (messages is null)
        {
            return null;
        }

        ChatMessage? lastUser = null;
        foreach (var m in messages)
        {
            if (m.Role == ChatRole.User)
            {
                lastUser = m;
            }
        }

        return lastUser?.Text;
    }
}
