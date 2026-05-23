namespace Sentirum.Agent;

/// <summary>
/// Wire protocol used to communicate with the MiniMax API.
/// </summary>
public enum MiniMaxProtocol
{
    /// <summary>
    /// OpenAI Chat Completions compatible protocol.
    /// Base URL: <c>https://api.minimax.io/v1</c>
    /// </summary>
    OpenAI,

    /// <summary>
    /// Anthropic Messages API compatible protocol.
    /// Base URL: <c>https://api.minimax.io/anthropic</c>
    /// </summary>
    Anthropic,
}
