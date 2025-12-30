namespace Traveler.Core.Models;

/// <summary>
/// Represents a Destiny 2 character (Titan, Hunter, or Warlock).
/// </summary>
public class CharacterInfo
{
    public long CharacterId { get; set; }
    public string ClassName { get; set; } = "";
    public string RaceName { get; set; } = "";
    public string GenderName { get; set; } = "";
    public int LightLevel { get; set; }
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

