using System;

namespace Sentirum.Agent.Providers.ZAI;

/// <summary>
/// Well-known Z.AI endpoints, kept in one place so callers don't need to
/// remember (or mistype) them.
/// </summary>
public static class ZaiEndpoints
{
    /// <summary>
    /// Z.AI's OpenAI-compatible endpoint.
    /// </summary>
    public static readonly Uri OpenAI = new("https://api.z.ai/api/paas/v4");

    /// <summary>
    /// Z.AI's Anthropic-compatible endpoint.
    /// </summary>
    public static readonly Uri Anthropic = new("https://api.z.ai/api/anthropic");
}
