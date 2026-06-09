using System;
using System.Collections.Generic;
using System.Linq;
using Sentirum.Agent.Workflows.HumanInTheLoop;

namespace Sentirum.Agent.CustomerSupport;

/// <summary>
/// Pre-configured approval gate helpers for support tickets.
/// </summary>
public static class SupportApprovalGate
{
    /// <summary>
    /// Creates an <see cref="ApprovalGate"/> with the given id.
    /// Use <see cref="ComposeRequest"/> to build the request composer
    /// for <c>WithApprovalGate</c>.
    /// </summary>
    public static ApprovalGate Create(string gateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gateId);
        return ApprovalGate.Create(gateId);
    }

    /// <summary>
    /// Builds a request composer that checks whether the maximum amount
    /// across recommendations exceeds <paramref name="threshold"/>.
    /// </summary>
    public static Func<IReadOnlyList<object?>, ApprovalRequest> ComposeRequest(decimal threshold = 100m)
    {
        return outputs =>
        {
            var recs = SupportWorkflowBuilder.ExtractRecommendations(outputs);
            var amounts = SupportWorkflowBuilder.ExtractAmounts(recs);
            var max = amounts.DefaultIfEmpty(0m).Max();

            return new ApprovalRequest(
                "support-approval",
                $"Support approval required (max ${max})",
                string.Join("\n", recs),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["maxAmount"] = max.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                CorrelationId: Guid.NewGuid().ToString("N"),
                RequestId: string.Empty);
        };
    }
}
