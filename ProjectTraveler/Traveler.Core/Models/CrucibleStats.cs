namespace Traveler.Core.Models;

/// <summary>
/// Model for Crucible PvP statistics
/// Populated from Bungie API GetHistoricalStats endpoint
/// </summary>
public class CrucibleStats
{
    /// <summary>Total Crucible kills</summary>
    public int Kills { get; set; }
    
    /// <summary>Total Crucible deaths</summary>
    public int Deaths { get; set; }
    
    /// <summary>Total Crucible assists</summary>
    public int Assists { get; set; }
    
    /// <summary>Kill/Death ratio (Kills / Deaths)</summary>
    public double KillDeathRatio { get; set; }
    
    /// <summary>Efficiency (Kills + Assists) / Deaths - what Bungie shows as KDA</summary>
    public double Efficiency { get; set; }
    
    /// <summary>Win percentage (0-100)</summary>
    public double WinRate { get; set; }
    
    /// <summary>Total matches played</summary>
    public int MatchesPlayed { get; set; }
    
    /// <summary>Total wins</summary>
    public int Wins { get; set; }
    
    /// <summary>Total losses</summary>
    public int Losses { get; set; }
    
    /// <summary>Total time played in seconds</summary>
    public long SecondsPlayed { get; set; }
    
    /// <summary>Formatted time played string (e.g., "30d 6h")</summary>
    public string TimePlayed 
    { 
        get
        {
            var ts = TimeSpan.FromSeconds(SecondsPlayed);
            if (ts.Days > 0)
                return $"{ts.Days}d {ts.Hours}h";
            else if (ts.Hours > 0)
                return $"{ts.Hours}h {ts.Minutes}m";
            else
                return $"{ts.Minutes}m";
        }
    }
    
    /// <summary>Best kill streak</summary>
    public int BestKillStreak { get; set; }
    
    /// <summary>Precision kills (headshots)</summary>
    public int PrecisionKills { get; set; }
    
    /// <summary>Average K/D per match</summary>
    public double AverageKillsPerMatch => MatchesPlayed > 0 ? (double)Kills / MatchesPlayed : 0;
}
