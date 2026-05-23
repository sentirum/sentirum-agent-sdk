namespace Sentirum.Agent.Providers.ZAI;

/// <summary>
/// Selects which wire protocol Sentirum uses to talk to Z.AI. Z.AI exposes
/// the same GLM model family on both an OpenAI-compatible and an
/// Anthropic-compatible endpoint.
/// </summary>
public enum ZaiProtocol
{
    /// <summary>
    /// Use the OpenAI-compatible endpoint
    /// (<c>https://api.z.ai/api/paas/v4</c>). This is the default and
    /// supports the full OpenAI Chat Completions surface, including tool
    /// calling and structured outputs.
    /// </summary>
    OpenAI = 0,

    /// <summary>
    /// Use the Anthropic-compatible endpoint
    /// (<c>https://api.z.ai/api/anthropic</c>). Auth is sent as a Bearer
    /// token (not the canonical <c>x-api-key</c> header).
    /// </summary>
    Anthropic = 1,
}
