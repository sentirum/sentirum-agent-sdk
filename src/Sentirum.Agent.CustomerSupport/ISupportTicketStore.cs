using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Sentirum.Agent.CustomerSupport;

/// <summary>
/// Persisted store for <see cref="SupportTicket"/> instances.
/// </summary>
public interface ISupportTicketStore
{
    /// <summary>
    /// Adds or updates a ticket.
    /// </summary>
    void Upsert(SupportTicket ticket);

    /// <summary>
    /// Retrieves a ticket by id.
    /// </summary>
    bool TryGet(string id, out SupportTicket? ticket);

    /// <summary>
    /// Gets all tickets.
    /// </summary>
    IReadOnlyList<SupportTicket> GetAll();
}

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ISupportTicketStore"/>.
/// </summary>
public sealed class InMemorySupportTicketStore : ISupportTicketStore
{
    private readonly ConcurrentDictionary<string, SupportTicket> _tickets = new();

    /// <inheritdoc />
    public void Upsert(SupportTicket ticket) => _tickets[ticket.Id] = ticket;

    /// <inheritdoc />
    public bool TryGet(string id, out SupportTicket? ticket) => _tickets.TryGetValue(id, out ticket);

    /// <inheritdoc />
    public IReadOnlyList<SupportTicket> GetAll() => _tickets.Values.OrderByDescending(t => t.CreatedAt).ToList();
}
