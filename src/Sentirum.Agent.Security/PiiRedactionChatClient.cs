using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Security;

/// <summary>
/// A <see cref="DelegatingChatClient"/> that redacts personally identifiable
/// information (PII) from outgoing messages using configurable regex patterns.
/// </summary>
public sealed class PiiRedactionChatClient : DelegatingChatClient
{
    private readonly IReadOnlyList<RedactionRule> _rules;
    private readonly string _replacement;

    /// <summary>
    /// Creates a client with the built-in rule set.
    /// </summary>
    public PiiRedactionChatClient(IChatClient innerClient, string replacement = "[REDACTED]")
        : this(innerClient, BuiltInRules(), replacement)
    {
    }

    /// <summary>
    /// Creates a client with a custom rule set.
    /// </summary>
    public PiiRedactionChatClient(
        IChatClient innerClient,
        IEnumerable<RedactionRule> rules,
        string replacement = "[REDACTED]")
        : base(innerClient)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToList().AsReadOnly();
        _replacement = replacement;
    }

    /// <inheritdoc />
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var redacted = messages.Select(Redact).ToList();
        return base.GetResponseAsync(redacted, options, cancellationToken);
    }

    /// <inheritdoc />
    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var redacted = messages.Select(Redact).ToList();
        return base.GetStreamingResponseAsync(redacted, options, cancellationToken);
    }

    private ChatMessage Redact(ChatMessage message)
    {
        if (string.IsNullOrEmpty(message.Text))
        {
            return message;
        }

        var text = message.Text;
        foreach (var rule in _rules)
        {
            text = rule.Regex.Replace(text, _replacement);
        }

        return new ChatMessage(message.Role, text)
        {
            AuthorName = message.AuthorName,
            RawRepresentation = message.RawRepresentation,
            AdditionalProperties = message.AdditionalProperties,
        };
    }

    /// <summary>
    /// Built-in rules covering common PII categories.
    /// </summary>
    public static IEnumerable<RedactionRule> BuiltInRules()
    {
        yield return new RedactionRule(
            "email",
            new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled));

        yield return new RedactionRule(
            "phone",
            new Regex(@"\b(?:\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b", RegexOptions.Compiled));

        yield return new RedactionRule(
            "credit-card",
            new Regex(@"\b(?:\d{4}[-\s]?){3}\d{4}\b", RegexOptions.Compiled));

        yield return new RedactionRule(
            "ssn",
            new Regex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled));

        yield return new RedactionRule(
            "ipv4",
            new Regex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.Compiled));
    }
}

/// <summary>
/// A named regex rule for PII redaction.
/// </summary>
public sealed class RedactionRule
{
    /// <summary>
    /// Human-readable name of the rule (e.g. "email", "ssn").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The compiled regex used for matching.
    /// </summary>
    public Regex Regex { get; }

    public RedactionRule(string name, Regex regex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(regex);
        Name = name;
        Regex = regex;
    }
}
