using System.Threading.Tasks;
using Traveler.Core.Models;

namespace Traveler.Core.Interfaces;

/// <summary>
/// Service for managing user preferences and application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current user settings.
    /// </summary>
    UserSettings CurrentSettings { get; }
    
    /// <summary>
    /// Loads settings from persistent storage.
    /// </summary>
    Task LoadAsync();
    
    /// <summary>
    /// Saves current settings to persistent storage.
    /// </summary>
    Task SaveAsync();
    
    /// <summary>
    /// Updates a specific setting and triggers save.
    /// </summary>
    Task UpdateSettingAsync<T>(string key, T value);
}
