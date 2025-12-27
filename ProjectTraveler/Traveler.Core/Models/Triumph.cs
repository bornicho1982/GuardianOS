using System;
using System.Collections.Generic;

namespace Traveler.Core.Models;

/// <summary>
/// Represents a Triumph/Record with recursive children for tree display.
/// Uses bitwise operations on DestinyRecordState for status determination.
/// </summary>
public record Triumph
{
    public uint Hash { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? IconPath { get; init; }
    
    /// <summary>
    /// Raw state value from DestinyRecordState enum.
    /// </summary>
    public int State { get; init; }
    
    /// <summary>
    /// Child presentation nodes (recursive tree structure).
    /// </summary>
    public List<Triumph> Children { get; init; } = new();
    
    /// <summary>
    /// Progress towards completion (objectives).
    /// </summary>
    public int Progress { get; init; }
    public int CompletionGoal { get; init; }
    
    // DestinyRecordState flags (from Bungie API)
    private const int RecordRedeemed = 1;
    private const int RewardUnavailable = 2;
    private const int ObjectiveNotCompleted = 4;
    private const int Obscured = 8;
    private const int Invisible = 16;
    private const int EntitlementUnowned = 32;
    private const int CanEquipTitle = 64;
    
    /// <summary>
    /// True if all objectives are completed.
    /// </summary>
    public bool IsCompleted => (State & ObjectiveNotCompleted) == 0;
    
    /// <summary>
    /// True if the triumph reward has been claimed.
    /// </summary>
    public bool IsRedeemed => (State & RecordRedeemed) != 0;
    
    /// <summary>
    /// True if triumph is hidden from the user.
    /// </summary>
    public bool IsHidden => (State & (Obscured | Invisible)) != 0;
    
    /// <summary>
    /// True if this is a title that can be equipped.
    /// </summary>
    public bool IsTitle => (State & CanEquipTitle) != 0;
    
    /// <summary>
    /// Completion percentage (0-100).
    /// </summary>
    public int CompletionPercent => CompletionGoal > 0 
        ? Math.Min(100, (int)(Progress * 100.0 / CompletionGoal)) 
        : (IsCompleted ? 100 : 0);
}
