namespace Traveler.Core.Models;

/// <summary>
/// Represents a Destiny 2 character (Titan, Hunter, or Warlock).
/// </summary>
public class CharacterInfo
{
    public string CharacterId { get; set; } = string.Empty;
    public string ClassType { get; set; } = string.Empty; // Titan, Hunter, Warlock
    public string ClassName 
    { 
        get => ClassType; 
        set => ClassType = value; 
    } // Backward compatibility alias
    public string RaceName { get; set; } = string.Empty;
    public string GenderName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty; 
    public int LightLevel { get; set; }
    
    // Power Breakdown
    public int BasePowerLevel { get; set; }
    public int ArtifactBonus { get; set; }
    public double PercentToNextLevel { get; set; } // 0.0 to 1.0
    public string PowerDisplay => $"{BasePowerLevel} + {ArtifactBonus}";

    // Season Pass
    public int SeasonRank { get; set; }
    public double SeasonProgressPercent { get; set; } // 0.0 to 1.0
    public string SeasonRewardIcon { get; set; } = string.Empty;
    public string EmblemPath { get; set; } = "";
    public string EmblemBackgroundPath { get; set; } = "";
    
    /// <summary>
    /// Full URL to the emblem icon on Bungie.net
    /// </summary>
    public string EmblemUrl => string.IsNullOrEmpty(EmblemPath) 
        ? "" 
        : $"https://www.bungie.net{EmblemPath}";
    
    /// <summary>
    /// Full URL to the emblem background on Bungie.net
    /// </summary>
    public string EmblemBackgroundUrl => string.IsNullOrEmpty(EmblemBackgroundPath) 
        ? "" 
        : $"https://www.bungie.net{EmblemBackgroundPath}";
    
    // Stats
    public int Mobility { get; set; }
    public int Resilience { get; set; }
    public int Recovery { get; set; }
    public int Discipline { get; set; }
    public int Intellect { get; set; }
    public int Strength { get; set; }
    
    /// <summary>
    /// Gets the class icon based on class name.
    /// </summary>
    public string ClassIcon => ClassName switch
    {
        "Titan" => "🛡️",
        "Hunter" => "🏹",
        "Warlock" => "🔮",
        _ => "⚔️"
    };
}

