using System.Collections.Generic;

namespace Sentirum.Agent.Sessions.Tree;

/// <summary>
/// A single node in a <see cref="SessionTree"/>.
/// </summary>
public sealed class SessionTreeNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionTreeNode"/> class.
    /// </summary>
    public SessionTreeNode(
        ISentirumSession session,
        int depth,
        int messageCount)
    {
        Session = session;
        Depth = depth;
        MessageCount = messageCount;
        Children = [];
    }

    /// <summary>
    /// Gets the underlying Sentirum session for this node.
    /// </summary>
    public ISentirumSession Session { get; }

    /// <summary>
    /// Gets the depth of this node, where the root is <c>0</c>.
    /// </summary>
    public int Depth { get; }

    /// <summary>
    /// Gets the number of messages in this session at the time of the snapshot.
    /// </summary>
    public int MessageCount { get; }

    /// <summary>
    /// Gets the direct children of this node, ordered by insertion.
    /// </summary>
    public List<SessionTreeNode> Children { get; }
}
