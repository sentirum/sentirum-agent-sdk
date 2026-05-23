using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Sentirum.Agent;

/// <summary>
/// Default <see cref="ISentirumAgentRegistry"/> implementation populated
/// from the DI container.
/// </summary>
public sealed class SentirumAgentRegistry : ISentirumAgentRegistry
{
    private readonly Dictionary<string, ISentirumAgent> _byId;

    /// <summary>
    /// Initializes a new instance of the <see cref="SentirumAgentRegistry"/> class.
    /// </summary>
    public SentirumAgentRegistry(IEnumerable<ISentirumAgent> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);

        _byId = agents.ToDictionary(a => a.Id, StringComparer.Ordinal);
        Agents = new ReadOnlyCollection<ISentirumAgent>(_byId.Values.ToList());
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ISentirumAgent> Agents { get; }

    /// <inheritdoc />
    public ISentirumAgent? Find(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        return _byId.TryGetValue(agentId, out var agent) ? agent : null;
    }
}
