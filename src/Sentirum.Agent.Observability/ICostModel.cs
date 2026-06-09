using System;
using System.Collections.Generic;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Observability;

/// <summary>
/// Computes the monetary cost of a chat request from token usage.
/// </summary>
public interface ICostModel
{
    /// <summary>
    /// Calculates the cost in USD for the supplied usage.
    /// </summary>
    /// <param name="modelId">The model identifier (e.g. <c>gpt-4o-mini</c>).</param>
    /// <param name="usage">Token usage reported by the provider.</param>
    /// <returns>Cost in USD.</returns>
    decimal CalculateCost(string? modelId, UsageDetails? usage);
}

/// <summary>
/// A simple per-model cost model with input and output token prices.
/// </summary>
public sealed class PerModelCostModel : ICostModel
{
    private readonly Dictionary<string, TokenPrice> _prices;

    /// <summary>
    /// Initializes a new instance with the supplied price table.
    /// </summary>
    /// <param name="prices">
    /// Keys are model identifiers; values are price-per-million-tokens.
    /// </param>
    public PerModelCostModel(IEnumerable<KeyValuePair<string, TokenPrice>> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);
        _prices = new Dictionary<string, TokenPrice>(prices, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a cost model from a dictionary of prices.
    /// </summary>
    public static PerModelCostModel Create(params (string Model, TokenPrice Price)[] entries)
    {
        var dict = new Dictionary<string, TokenPrice>(StringComparer.OrdinalIgnoreCase);
        foreach (var (model, price) in entries)
        {
            dict[model] = price;
        }
        return new PerModelCostModel(dict);
    }

    /// <inheritdoc />
    public decimal CalculateCost(string? modelId, UsageDetails? usage)
    {
        if (usage is null || string.IsNullOrEmpty(modelId))
        {
            return 0m;
        }

        if (!_prices.TryGetValue(modelId, out var price))
        {
            return 0m;
        }

        var inputCost = (usage.InputTokenCount.GetValueOrDefault() / 1_000_000m) * price.InputPricePerMillion;
        var outputCost = (usage.OutputTokenCount.GetValueOrDefault() / 1_000_000m) * price.OutputPricePerMillion;
        return inputCost + outputCost;
    }
}

/// <summary>
/// Price-per-million-tokens for a specific model.
/// </summary>
public readonly record struct TokenPrice(
    decimal InputPricePerMillion,
    decimal OutputPricePerMillion);
