using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sentirum.Agent.Memory.EntityFrameworkCore;

/// <summary>
/// EF Core entity representing a single Sentirum memory row. Indexed by
/// (Scope, AgentId, UserId, SessionId, Key) so partition queries are
/// covered.
/// </summary>
[Table("SentirumMemory")]
public class SentirumMemoryRecord
{
    /// <summary>Surrogate key.</summary>
    [Key]
    public long Id { get; set; }

    /// <summary>Memory scope (0=Global, 1=Agent, 2=User, 3=Session).</summary>
    public int Scope { get; set; }

    /// <summary>Agent id, when applicable.</summary>
    [MaxLength(200)]
    public string? AgentId { get; set; }

    /// <summary>User id, when applicable.</summary>
    [MaxLength(200)]
    public string? UserId { get; set; }

    /// <summary>Session id, when applicable.</summary>
    [MaxLength(200)]
    public string? SessionId { get; set; }

    /// <summary>Logical key within the partition.</summary>
    [Required]
    [MaxLength(400)]
    public string Key { get; set; } = string.Empty;

    /// <summary>Opaque payload.</summary>
    [Required]
    public string Value { get; set; } = string.Empty;

    /// <summary>UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>UTC last-update timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optional absolute expiration.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }
}
