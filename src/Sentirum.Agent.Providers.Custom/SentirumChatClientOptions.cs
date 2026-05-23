using System;

namespace Sentirum.Agent.Providers.Custom;

/// <summary>
/// Behavioral knobs for <see cref="SentirumChatClientBase"/>. Controls retry,
/// timeout, and logging policies applied around every provider call.
/// </summary>
public sealed class SentirumChatClientOptions
{
    /// <summary>
    /// Gets a default options instance with sensible production values.
    /// </summary>
    public static SentirumChatClientOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets a friendly provider name used in logs and metrics.
    /// </summary>
    public string ProviderName { get; set; } = "custom";

    /// <summary>
    /// Gets or sets the per-call timeout. <see cref="TimeSpan.Zero"/> disables.
    /// Defaults to 120 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Gets or sets how many times a failed call is retried (in addition to
    /// the original attempt). Set to <c>0</c> to disable retries. Defaults to <c>3</c>.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay used for exponential backoff between
    /// retries. Defaults to 500 ms.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Gets or sets whether to log request and response message counts at
    /// <c>Information</c> level. Defaults to <see langword="true"/>.
    /// </summary>
    public bool LogRequests { get; set; } = true;
}
