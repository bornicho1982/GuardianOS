using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Traveler.Core.Interfaces;
using Traveler.Core.Models;

namespace Traveler.Data.Services.Settings;

/// <summary>
/// Service for managing user preferences persisted to local JSON file.
/// </summary>
public class SettingsService : ISettingsService
{
    private static readonly string SettingsDirectory = 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GuardianOS");
    private static readonly string SettingsFilePath = 
        Path.Combine(SettingsDirectory, "settings.json");

    private UserSettings _settings = new();

    public UserSettings CurrentSettings => _settings;

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                // Create default settings
                _settings = new UserSettings();
                await SaveAsync();
                return;
            }

            var json = await File.ReadAllTextAsync(SettingsFilePath);
            var loaded = JsonSerializer.Deserialize<UserSettings>(json);
            _settings = loaded ?? new UserSettings();
            
            Console.WriteLine($"Settings loaded from {SettingsFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings: {ex.Message}");
            _settings = new UserSettings();
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(SettingsDirectory);

            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(SettingsFilePath, json);
            
            Console.WriteLine($"Settings saved to {SettingsFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    public async Task UpdateSettingAsync<T>(string key, T value)
    {
        // Use reflection to update property by name
        var property = typeof(UserSettings).GetProperty(key);
        if (property != null && property.CanWrite)
        {
            property.SetValue(_settings, value);
            await SaveAsync();
        }
        else
        {
            Console.WriteLine($"Setting '{key}' not found or not writable.");
        }
    }
}
