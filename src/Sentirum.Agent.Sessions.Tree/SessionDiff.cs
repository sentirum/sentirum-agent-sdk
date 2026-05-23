namespace Sentirum.Agent.Sessions.Tree;

/// <summary>
/// Structural difference between two sessions. Returned by
/// <see cref="ITreeSessionStore.CompareAsync"/>.
/// </summary>
public sealed class SessionDiff
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionDiff"/> class.
    /// </summary>
    public SessionDiff(
        string leftSessionId,
        string rightSessionId,
        int leftMessageCount,
        int rightMessageCount)
    {
        LeftSessionId = leftSessionId;
        RightSessionId = rightSessionId;
        LeftMessageCount = leftMessageCount;
        RightMessageCount = rightMessageCount;
    }

    /// <summary>Gets the identifier of the left-hand session.</summary>
    public string LeftSessionId { get; }

    /// <summary>Gets the identifier of the right-hand session.</summary>
    public string RightSessionId { get; }

    /// <summary>Gets the message count on the left-hand session.</summary>
    public int LeftMessageCount { get; }

    /// <summary>Gets the message count on the right-hand session.</summary>
    public int RightMessageCount { get; }

    /// <summary>
    /// Gets the signed delta <c>left - right</c>. Positive means the left
    /// branch grew more after the fork.
    /// </summary>
    public int MessageCountDelta => LeftMessageCount - RightMessageCount;

    /// <inheritdoc />
    public override string ToString() =>
        $"diff({LeftSessionId} vs {RightSessionId}): " +
        $"msgs={LeftMessageCount}/{RightMessageCount} (Δ={MessageCountDelta:+#;-#;0})";
}
