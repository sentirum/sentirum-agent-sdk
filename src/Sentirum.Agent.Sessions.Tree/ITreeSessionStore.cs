using System.Threading;
using System.Threading.Tasks;

namespace Sentirum.Agent.Sessions.Tree;

/// <summary>
/// Extends <see cref="ISentirumSessionStore"/> with the tree-shaped operations
/// that make Sentirum's branching sessions possible: deep-copy forks, branch
/// merges, tree traversal, and branch comparison.
/// </summary>
public interface ITreeSessionStore : ISentirumSessionStore
{
    /// <summary>
    /// Resolves the entire session tree rooted at <paramref name="rootSessionId"/>.
    /// </summary>
    /// <param name="rootSessionId">
    /// The identifier of any session in the tree. The tree is always returned
    /// from its root, regardless of which node was supplied.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<SessionTree> GetTreeAsync(
        string rootSessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges the conversation from <paramref name="source"/> into
    /// <paramref name="target"/> by replaying the last <paramref name="lastMessageCount"/>
    /// messages produced after the divergence point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Merge is intentionally lossy: it transfers <em>messages</em>, not
    /// provider-specific state such as server-side conversation IDs.
    /// </para>
    /// <para>
    /// When <paramref name="lastMessageCount"/> is <c>null</c>, every message
    /// added on the source branch after the fork point is replayed onto the
    /// target.
    /// </para>
    /// </remarks>
    Task MergeAsync(
        ISentirumSession source,
        ISentirumSession target,
        int? lastMessageCount = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces a structural diff between two sessions: message counts,
    /// depth, and (when available) token-usage deltas.
    /// </summary>
    Task<SessionDiff> CompareAsync(
        ISentirumSession left,
        ISentirumSession right,
        CancellationToken cancellationToken = default);
}
