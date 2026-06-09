using System;
using System.Collections.Generic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Sentirum.Agent.Security;

/// <summary>
/// Builder extensions for adding security layers to a Sentirum agent.
/// </summary>
public static class SentirumSecurityBuilderExtensions
{
    /// <summary>
    /// Adds PII redaction to the agent's outgoing messages using the built-in
    /// rule set (email, phone, credit card, SSN, IPv4).
    /// </summary>
    public static ISentirumAgentBuilder WithPiiRedaction(
        this ISentirumAgentBuilder builder,
        string replacement = "[REDACTED]")
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureChatClient(b => b.Use(client =>
            new PiiRedactionChatClient(client, replacement)));

        return builder;
    }

    /// <summary>
    /// Adds PII redaction with a custom rule set.
    /// </summary>
    public static ISentirumAgentBuilder WithPiiRedaction(
        this ISentirumAgentBuilder builder,
        IEnumerable<RedactionRule> rules,
        string replacement = "[REDACTED]")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(rules);

        builder.ConfigureChatClient(b => b.Use(client =>
            new PiiRedactionChatClient(client, rules, replacement)));

        return builder;
    }

    /// <summary>
    /// Adds content safety scanning to the agent. Requires an
    /// <see cref="IContentSafetyClient"/> to be registered in DI.
    /// </summary>
    public static ISentirumAgentBuilder WithContentSafety(
        this ISentirumAgentBuilder builder,
        ContentSafetyThreshold threshold = ContentSafetyThreshold.Medium)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureChatClient(b => b.Use((client, services) =>
        {
            var safetyClient = services.GetRequiredService<IContentSafetyClient>();
            return new ContentSafetyChatClient(client, safetyClient, threshold);
        }));

        return builder;
    }
}
