using System;
using Microsoft.Extensions.DependencyInjection;
using Sentirum.Agent.Builder;
using Sentirum.Agent.Context;
using Sentirum.Agent.CustomerSupport.Sentiment;
using Sentirum.Agent.Security;

namespace Sentirum.Agent.CustomerSupport;

/// <summary>
/// Opinionated fluent builder that configures a customer-support agent on
/// top of <see cref="ISentirumAgentBuilder"/>. It layers support-specific
/// conveniences — tier/escalation personas, PII redaction, knowledge-base
/// grounding, and sentiment-based escalation — while still exposing the
/// underlying builder for any standard extension.
/// </summary>
/// <remarks>
/// <para>
/// Typical usage:
/// </para>
/// <code>
/// services.AddSentirumSupportAgent("tier1", b => b
///     .UseZAI("glm-4.6", apiKey: key)
///     .WithTier1()
///     .WithPiiRedaction()
///     .WithKnowledgeBase&lt;SupportDocs&gt;()
///     .WithSentimentBasedEscalation());
/// </code>
/// <para>
/// Every method returns the same <see cref="SupportAgentBuilder"/> so calls
/// chain. An implicit conversion to <see cref="ISentirumAgentBuilder"/>
/// keeps it compatible with any existing builder extension.
/// </para>
/// </remarks>
public sealed class SupportAgentBuilder
{
    private readonly ISentirumAgentBuilder _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="SupportAgentBuilder"/> class.
    /// </summary>
    /// <param name="inner">The underlying Sentirum agent builder.</param>
    public SupportAgentBuilder(ISentirumAgentBuilder inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>
    /// The underlying builder, in case a caller needs a standard extension
    /// that this opinionated builder does not surface.
    /// </summary>
    public ISentirumAgentBuilder Inner => _inner;

    /// <summary>The logical agent name.</summary>
    public string Name => _inner.Name;

    // ── Standard delegations (so the support builder is a superset) ─────

    /// <summary>Sets the agent's system instructions.</summary>
    public SupportAgentBuilder WithInstructions(string instructions)
    {
        _inner.WithInstructions(instructions);
        return this;
    }

    /// <summary>Sets the agent's human-readable description.</summary>
    public SupportAgentBuilder WithDescription(string description)
    {
        _inner.WithDescription(description);
        return this;
    }

    // ── Support personas ────────────────────────────────────────────────

    /// <summary>
    /// Applies the default Tier-1 responder persona (friendly,
    /// resolution-focused, scope-aware).
    /// </summary>
    public SupportAgentBuilder WithTier1()
        => WithInstructions(SupportAgentInstructions.Tier1);

    /// <summary>
    /// Applies the escalation-specialist persona (wider remedy authority,
    /// empathy-first).
    /// </summary>
    /// <param name="instructions">
    /// Optional override. When <see langword="null"/>, the default
    /// escalation persona is used.
    /// </param>
    public SupportAgentBuilder WithEscalation(string? instructions = null)
        => WithInstructions(instructions ?? SupportAgentInstructions.Escalation);

    // ── Security ────────────────────────────────────────────────────────

    /// <summary>
    /// Adds PII redaction to the agent's chat-client pipeline using the
    /// built-in rule set (email, phone, credit card, SSN, IPv4).
    /// </summary>
    /// <param name="replacement">The placeholder text for redacted spans.</param>
    public SupportAgentBuilder WithPiiRedaction(string replacement = "[REDACTED]")
    {
        _inner.WithPiiRedaction(replacement);
        return this;
    }

    // ── Knowledge base ──────────────────────────────────────────────────

    /// <summary>
    /// Grounds the agent in a DI-registered knowledge base, injecting the
    /// top snippets seeded from the latest user message.
    /// </summary>
    public SupportAgentBuilder WithKnowledgeBase<TKnowledgeBase>(
        int maxResults = 3,
        string heading = "Relevant support knowledge:")
        where TKnowledgeBase : class, IKnowledgeBase
    {
        _inner.WithKnowledgeBase<TKnowledgeBase>(maxResults, heading);
        return this;
    }

    /// <summary>
    /// Grounds the agent in an explicit knowledge-base instance.
    /// </summary>
    public SupportAgentBuilder WithKnowledgeBase(
        IKnowledgeBase knowledgeBase,
        int maxResults = 3,
        string heading = "Relevant support knowledge:")
    {
        _inner.WithKnowledgeBase(knowledgeBase, maxResults, heading);
        return this;
    }

    // ── Sentiment-based escalation ──────────────────────────────────────

    /// <summary>
    /// Wires a <see cref="SentimentEscalationContextProvider"/> that scores
    /// each customer turn and injects an escalation instruction when the
    /// sentiment is confidently negative.
    /// </summary>
    /// <param name="configure">
    /// Optional callback to tune the threshold, instructions, analyzer, or
    /// to attach a per-turn callback.
    /// </param>
    public SupportAgentBuilder WithSentimentBasedEscalation(
        Action<SentimentEscalationOptions>? configure = null)
    {
        var options = new SentimentEscalationOptions();
        configure?.Invoke(options);
        options.Validate();

        // Defer the analyzer resolution to the options pipeline (ADR-0005)
        // so a DI-registered ISentimentAnalyzer is picked up, falling back
        // to the dependency-free KeywordSentimentAnalyzer.
        _inner.Configure(agentOptions =>
        {
            var analyzer = options.Analyzer;
            if (analyzer is null)
            {
                var sp = SentirumServiceProviderAccessor.Current;
                analyzer = sp?.GetService<ISentimentAnalyzer>() ?? KeywordSentimentAnalyzer.Instance;
            }
            agentOptions.ContextProviders.Add(new SentimentEscalationContextProvider(analyzer, options));
        });

        return this;
    }
}
