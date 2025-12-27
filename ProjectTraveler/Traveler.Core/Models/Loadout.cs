using System;
using System.Collections.Generic;

namespace Traveler.Core.Models;

/// <summary>
/// Represents a user-defined loadout configuration.
/// </summary>
public record Loadout
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "New Loadout";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Selected armor piece instance IDs by bucket hash.
    /// </summary>
    public Dictionary<uint, long> ArmorPieces { get; set; } = new();
    
    /// <summary>
    /// Selected weapon instance IDs by bucket hash.
    /// </summary>
    public Dictionary<uint, long> Weapons { get; set; } = new();
    
    /// <summary>
    /// Subclass hash (if part of loadout).
    /// </summary>
    public uint? SubclassHash { get; set; }
    
    /// <summary>
    /// Tier 5 tuning configurations (InstanceId -> "Plus,Minus" stat hashes).
    /// </summary>
    public Dictionary<long, (uint PlusStat, uint MinusStat)> TuningConfig { get; set; } = new();
    
    /// <summary>
    /// Constraint used to generate this loadout (for re-optimization).
    /// </summary>
    public LoadoutConstraints? OriginalConstraints { get; set; }
}

/// <summary>
/// User-defined constraints for loadout optimization.
/// </summary>
public record LoadoutConstraints
{
    /// <summary>
    /// Forced exotic armor piece (item hash).
    /// </summary>
    public uint? LockedExoticHash { get; set; }
    
    /// <summary>
    /// Minimum stat requirements (stat hash -> min value, 0-200 scale).
    /// </summary>
    public Dictionary<uint, int> MinimumStats { get; set; } = new();
    
    /// <summary>
    /// Natural language query for AI interpretation.
    /// </summary>
    public string? NaturalLanguageQuery { get; set; }
}
