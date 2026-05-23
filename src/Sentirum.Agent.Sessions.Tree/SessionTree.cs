using System.Collections.Generic;
using System.Text;

namespace Sentirum.Agent.Sessions.Tree;

/// <summary>
/// A snapshot of a Sentirum session tree. Returned by
/// <see cref="ITreeSessionStore.GetTreeAsync"/>.
/// </summary>
public sealed class SessionTree
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionTree"/> class.
    /// </summary>
    public SessionTree(SessionTreeNode root)
    {
        Root = root;
    }

    /// <summary>
    /// Gets the root of the session tree.
    /// </summary>
    public SessionTreeNode Root { get; }

    /// <summary>
    /// Enumerates every node in the tree in breadth-first order, starting
    /// from <see cref="Root"/>.
    /// </summary>
    public IEnumerable<SessionTreeNode> WalkBreadthFirst()
    {
        var queue = new Queue<SessionTreeNode>();
        queue.Enqueue(Root);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            yield return node;

            foreach (var child in node.Children)
            {
                queue.Enqueue(child);
            }
        }
    }

    /// <summary>
    /// Enumerates every node in the tree in depth-first order, starting
    /// from <see cref="Root"/>.
    /// </summary>
    public IEnumerable<SessionTreeNode> WalkDepthFirst()
    {
        var stack = new Stack<SessionTreeNode>();
        stack.Push(Root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;

            // Reverse-push so the first child is visited first.
            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(node.Children[i]);
            }
        }
    }

    /// <summary>
    /// Renders an ASCII view of the tree useful for logs and debugging.
    /// </summary>
    public string ToAsciiTree()
    {
        var builder = new StringBuilder();
        RenderNode(Root, prefix: string.Empty, isLast: true, builder);
        return builder.ToString();
    }

    private static void RenderNode(
        SessionTreeNode node,
        string prefix,
        bool isLast,
        StringBuilder builder)
    {
        builder.Append(prefix);
        builder.Append(isLast ? "└── " : "├── ");
        builder.Append(node.Session.Id);

        if (node.MessageCount > 0)
        {
            builder.Append(" (").Append(node.MessageCount).Append(" msgs)");
        }

        builder.AppendLine();

        var childPrefix = prefix + (isLast ? "    " : "│   ");
        for (var i = 0; i < node.Children.Count; i++)
        {
            RenderNode(node.Children[i], childPrefix, i == node.Children.Count - 1, builder);
        }
    }
}
