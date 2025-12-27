namespace Traveler.Core.Models;

/// <summary>
/// User preferences persisted to local storage.
/// </summary>
public class UserSettings
{
    /// <summary>
    /// Language code for Manifest (e.g., "en", "es", "fr").
    /// </summary>
    public string Language { get; set; } = "en";
    
    /// <summary>
    /// UI Theme: "Dark" or "Light".
    /// </summary>
    public string Theme { get; set; } = "Dark";
    
    /// <summary>
    /// Ollama server endpoint for local AI.
    /// </summary>
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    
    /// <summary>
    /// AI model to use (e.g., "phi3", "llama2").
    /// </summary>
    public string AiModel { get; set; } = "phi3";
    
    /// <summary>
    /// Path to external wishlist JSON file.
    /// </summary>
    public string? WishlistPath { get; set; }
    
    /// <summary>
    /// Show completed triumphs in tree.
    /// </summary>
    public bool ShowCompletedTriumphs { get; set; } = true;
    
    /// <summary>
    /// Enable item comparison tooltips.
    /// </summary>
    public bool EnableItemComparison { get; set; } = true;
    
    /// <summary>
    /// Default character ID for quick operations.
    /// </summary>
    public long? DefaultCharacterId { get; set; }
}
