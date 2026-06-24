using System;
using System.Collections.Generic;
using System.Linq;
using Sentirum.Agent.Workflows;

namespace Sentirum.Agent.CustomerSupport;

/// <summary>
/// Factory for creating pre-configured customer-support workflows.
/// </summary>
public static class SupportWorkflowBuilder
{
    /// <summary>
    /// Creates a standard triage workflow that runs specialist agents in
    /// parallel and aggregates their recommendations.
    /// </summary>
    /// <param name="name">Workflow name.</param>
    /// <param name="specialists">The specialist agents to run concurrently.</param>
    public static ISentirumWorkflow CreateTriageWorkflow(
        string name,
        IEnumerable<ISentirumAgent> specialists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(specialists);

        var list = specialists.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("At least one specialist is required.", nameof(specialists));
        }

        return Workflows.SentirumWorkflowBuilder.Create(name)
            .WithName($"{name} Triage")
            .ConcurrentJoin(list)
            .Build();
    }

    /// <summary>
    /// Parses recommendation strings to extract monetary amounts.
    /// Supports $ or € prefixes and decimal values.
    /// </summary>
    public static IEnumerable<decimal> ExtractAmounts(IEnumerable<string> recommendations)
    {
        foreach (var rec in recommendations)
        {
            if (string.IsNullOrWhiteSpace(rec))
            {
                continue;
            }

            var parts = rec.Split([' ', ',', ';']);
            foreach (var part in parts)
            {
                var cleaned = part.Trim().TrimStart('$', '€', '£');
                if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var amount))
                {
                    yield return amount;
                }
            }
        }
    }

    /// <summary>
    /// Extracts the text output from each agent in a workflow result.
    /// </summary>
    public static List<string> ExtractRecommendations(IReadOnlyList<object?> outputs)
    {
        var result = new List<string>(outputs.Count);
        foreach (var output in outputs)
        {
            if (output is string s)
            {
                result.Add(s);
            }
            else if (output is not null)
            {
                result.Add(output.ToString() ?? string.Empty);
            }
        }
        return result;
    }
}
