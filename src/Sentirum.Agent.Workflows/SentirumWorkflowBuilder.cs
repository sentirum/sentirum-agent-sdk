using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace Sentirum.Agent.Workflows;

/// <summary>
/// Fluent builder for <see cref="ISentirumWorkflow"/>. Wraps MAF's
/// <see cref="AgentWorkflowBuilder"/> and <see cref="WorkflowBuilder"/>
/// so the common Sentirum scenarios — sequential pipelines, concurrent
/// fan-out with an aggregator, agent handoffs — read as one-liners over
/// <see cref="ISentirumAgent"/>.
/// </summary>
/// <remarks>
/// <para>
/// Three shapes are first-class:
/// </para>
/// <list type="bullet">
///   <item><description><b>Sequential</b> — agents run head-to-tail; each
///   sees the previous agent's output as its input.</description></item>
///   <item><description><b>Concurrent</b> — agents run in parallel; an
///   aggregator collapses their outputs.</description></item>
///   <item><description><b>Handoff</b> — an initial agent delegates the
///   turn to a peer via tool calls (MAF's handoff workflow).</description></item>
/// </list>
/// <para>
/// Anything more exotic — switch routing, fan-in barriers, custom
/// executors — is still reachable via <see cref="UseWorkflow"/> which
/// lets callers hand in a fully-built MAF <see cref="Workflow"/>.
/// </para>
/// </remarks>
public sealed class SentirumWorkflowBuilder
{
    private readonly string _id;
    private string _name;
    private Workflow? _workflow;
    private WorkflowDispatchMode _dispatchMode = WorkflowDispatchMode.AgentTurn;

    private SentirumWorkflowBuilder(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        _id = id;
        _name = id;
    }

    /// <summary>
    /// Starts a new builder. <paramref name="id"/> is used for telemetry
    /// and as the default display name.
    /// </summary>
    public static SentirumWorkflowBuilder Create(string id) => new(id);

    /// <summary>
    /// Overrides the human-friendly display name.
    /// </summary>
    public SentirumWorkflowBuilder WithName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
        return this;
    }

    /// <summary>
    /// Builds a sequential agent pipeline: each agent runs in order and
    /// receives the previous agent's output as its input. Equivalent to
    /// <see cref="AgentWorkflowBuilder.BuildSequential(string, IEnumerable{AIAgent})"/>.
    /// </summary>
    public SentirumWorkflowBuilder Sequential(params ISentirumAgent[] agents)
        => Sequential((IEnumerable<ISentirumAgent>)agents);

    /// <summary>
    /// Builds a sequential agent pipeline.
    /// </summary>
    public SentirumWorkflowBuilder Sequential(IEnumerable<ISentirumAgent> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);
        var inner = ResolveInnerAgents(agents);
        _workflow = AgentWorkflowBuilder.BuildSequential(_name, inner);
        return this;
    }

    /// <summary>
    /// Builds a concurrent fan-out workflow: every supplied agent runs in
    /// parallel against the same input and <paramref name="aggregator"/>
    /// collapses each branch's <c>List&lt;ChatMessage&gt;</c> output into
    /// a single merged conversation. Mirrors
    /// <see cref="AgentWorkflowBuilder.BuildConcurrent(string, IEnumerable{AIAgent}, Func{IList{List{Microsoft.Extensions.AI.ChatMessage}}, List{Microsoft.Extensions.AI.ChatMessage}})"/>.
    /// </summary>
    public SentirumWorkflowBuilder Concurrent(
        IEnumerable<ISentirumAgent> agents,
        Func<IList<List<Microsoft.Extensions.AI.ChatMessage>>, List<Microsoft.Extensions.AI.ChatMessage>> aggregator)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(aggregator);
        var inner = ResolveInnerAgents(agents);
        _workflow = AgentWorkflowBuilder.BuildConcurrent(_name, inner, aggregator);
        return this;
    }

    /// <summary>
    /// Convenience overload that flattens every branch into a single
    /// assistant message whose text is the branch responses joined with
    /// <paramref name="separator"/>. Convenient for triage scenarios
    /// where each branch produces independently useful output and the
    /// caller wants the full set in one envelope.
    /// </summary>
    public SentirumWorkflowBuilder ConcurrentJoin(
        IEnumerable<ISentirumAgent> agents,
        string separator = "\n\n")
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(separator);
        return Concurrent(agents, branches =>
        {
            var text = string.Join(
                separator,
                branches
                    .Select(b => string.Join("\n", b.Select(m => m.Text)))
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

            return new List<Microsoft.Extensions.AI.ChatMessage>
            {
                new(Microsoft.Extensions.AI.ChatRole.Assistant, text),
            };
        });
    }

    /// <summary>
    /// Builds a handoff workflow rooted at <paramref name="initialAgent"/>
    /// where each <paramref name="handoffs"/> entry can be reached via a
    /// tool call. Useful for "triage + specialists" topologies.
    /// </summary>
    public SentirumWorkflowBuilder Handoff(
        ISentirumAgent initialAgent,
        params ISentirumAgent[] handoffs)
    {
        ArgumentNullException.ThrowIfNull(initialAgent);
        ArgumentNullException.ThrowIfNull(handoffs);

        if (handoffs.Length == 0)
        {
            throw new ArgumentException("At least one handoff target is required.", nameof(handoffs));
        }

        var targets = new AIAgent[handoffs.Length];
        for (var i = 0; i < handoffs.Length; i++)
        {
            var target = handoffs[i] ?? throw new ArgumentException(
                "Handoff targets must not contain null entries.",
                nameof(handoffs));
            targets[i] = target.InnerAgent;
        }

        // MAAIW001: MAF marks handoff builders as evaluation-only in
        // 1.6.2 — the shape is stable enough for our scenarios (see
        // ADR-0011) and any breaking change shows up as a build error
        // we will react to.
#pragma warning disable MAAIW001
        var builder = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(initialAgent.InnerAgent)
            .WithName(_name)
            .WithHandoffs(initialAgent.InnerAgent, targets);

        _workflow = builder.Build();
#pragma warning restore MAAIW001
        return this;
    }

    /// <summary>
    /// Escape hatch: hand in a fully-built MAF <see cref="Workflow"/>.
    /// Lets callers compose advanced topologies (switches, fan-in
    /// barriers, sub-workflows) with the native API while still getting
    /// the Sentirum wrapper for the run surface.
    /// </summary>
    public SentirumWorkflowBuilder UseWorkflow(Workflow workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        _workflow = workflow;
        // Custom topologies route their input directly; do not auto-send
        // a TurnToken.
        _dispatchMode = WorkflowDispatchMode.Direct;
        return this;
    }

    /// <summary>
    /// Finalizes construction.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no topology was supplied (<c>Sequential</c>,
    /// <c>Concurrent</c>, <c>Handoff</c>, or <c>UseWorkflow</c>).
    /// </exception>
    public ISentirumWorkflow Build()
    {
        if (_workflow is null)
        {
            throw new InvalidOperationException(
                "Cannot build a workflow without a topology. Call one of " +
                "Sequential / Concurrent / Handoff / UseWorkflow first.");
        }
        return new SentirumWorkflow(_id, _name, _workflow, _dispatchMode);
    }

    private static AIAgent[] ResolveInnerAgents(IEnumerable<ISentirumAgent> agents)
    {
        var list = new List<AIAgent>();
        foreach (var a in agents)
        {
            if (a is null)
            {
                throw new ArgumentException(
                    "Agent list must not contain null entries.",
                    nameof(agents));
            }
            list.Add(a.InnerAgent);
        }
        if (list.Count == 0)
        {
            throw new ArgumentException("Agent list must not be empty.", nameof(agents));
        }
        return list.ToArray();
    }
}
