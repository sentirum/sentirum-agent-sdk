using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Context;

/// <summary>
/// <see cref="AIContextProvider"/> that contributes ambient system
/// instructions computed per request from the
/// <see cref="AIContextProvider.InvokingContext"/>.
/// </summary>
/// <remarks>
/// Use this when the instructions depend on the user, the session, or any
/// other ambient signal that is not available at agent-build time. For
/// static instructions use <see cref="SentirumAgentOptions.Instructions"/>.
/// </remarks>
public sealed class InstructionsContextProvider : MessageAIContextProvider
{
    private readonly Func<MessageAIContextProvider.InvokingContext, CancellationToken, ValueTask<string?>> _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstructionsContextProvider"/> class.
    /// </summary>
    public InstructionsContextProvider(
        Func<MessageAIContextProvider.InvokingContext, CancellationToken, ValueTask<string?>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    /// <inheritdoc />
    protected override async ValueTask<System.Collections.Generic.IEnumerable<ChatMessage>> ProvideMessagesAsync(
        MessageAIContextProvider.InvokingContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var instructions = await _factory(context, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(instructions))
        {
            return Array.Empty<ChatMessage>();
        }

        return new[]
        {
            new ChatMessage(ChatRole.System, instructions),
        };
    }
}
