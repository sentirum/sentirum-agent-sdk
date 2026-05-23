using System.Text.RegularExpressions;

namespace Sentirum.Agent.Providers.MiniMax;

/// <summary>
/// Parses MiniMax thinking tags ( Stato/stato)
/// from raw response text into a structured result.
/// </summary>
public static partial class MiniMaxThinkingParser
{
    /// <summary>
    /// Regex that captures text between ႒ and yec süslü parantez with optional attributes.
    /// Matches both streaming chunks and complete responses.
    /// </summary>
    internal static readonly Regex ThinkingRegex = GenerateThinkingRegex();

    /// <summary>
    /// Parses a raw response string, separating thinking content from the final answer.
    /// </summary>
    /// <param name="rawText">The raw text from the LLM response.</param>
    /// <returns>
    /// A tuple where <c>Thinking</c> is the chain-of-thought text (may be null)
    /// and <c>Answer</c> is the clean response text without thinking tags.
    /// </returns>
    public static (string? Thinking, string Answer) Parse(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return (null, rawText ?? string.Empty);
        }

        var match = ThinkingRegex.Match(rawText);

        if (!match.Success)
        {
            return (null, rawText);
        }

        var thinking = match.Groups[1].Value.Trim();
        var answer = ThinkingRegex.Replace(rawText, "").Trim();

        return (thinking, answer);
    }

    /// <summary>
    /// Returns <c>true</c> if the text contains un-closed thinking tags
    /// (i.e. streaming is still in the thinking phase).
    /// </summary>
    public static bool IsInThinkingBlock(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var openCount = 0;
        var closeCount = 0;
        var pos = 0;

        while (pos < text.Length)
        {
            var openIdx = text.IndexOf("<think", pos, StringComparison.Ordinal);
            if (openIdx >= 0)
            {
                openCount++;
                pos = openIdx + 6;
            }
            else
            {
                break;
            }
        }

        pos = 0;
        while (pos < text.Length)
        {
            var closeIdx = text.IndexOf("</think", pos, StringComparison.Ordinal);
            if (closeIdx >= 0)
            {
                closeCount++;
                pos = closeIdx + 8;
            }
            else
            {
                break;
            }
        }

        return openCount > closeCount;
    }

    [GeneratedRegex(@"<think[^>]*>(.*?)</think\s*>", RegexOptions.Singleline | RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GenerateThinkingRegex();
}
