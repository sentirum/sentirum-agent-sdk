using System.Threading;
using System.Threading.Tasks;

namespace Sentirum.Agent;

/// <summary>
/// Persists and retrieves <see cref="ISentirumSession"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// Sentirum supports tree-based sessions. Forking is modeled by creating a
/// new session whose <see cref="ISentirumSession.ParentId"/> points at the
/// session being forked from. Implementations decide whether forks share
/// state-on-write or are eagerly copied.
/// </para>
/// </remarks>
public interface ISentirumSessionStore
{
    /// <summary>
    /// Creates a new, empty session for the given agent.
    /// </summary>
    Task<ISentirumSession> CreateAsync(
        string agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new session that forks from the supplied parent session.
    /// </summary>
    Task<ISentirumSession> ForkAsync(
        ISentirumSession parent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads an existing session by identifier, or returns <see langword="null"/>
    /// when no such session exists.
    /// </summary>
    Task<ISentirumSession?> GetAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists any pending changes for the supplied session.
    /// </summary>
    Task SaveAsync(
        ISentirumSession session,
        CancellationToken cancellationToken = default);
}
