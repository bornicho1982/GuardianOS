using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using ReactiveUI;
using Traveler.Core.Interfaces;
using Traveler.Core.Models;

namespace Traveler.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Settings view with language, theme, and AI configuration.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    
    private string _language = "en";
    private string _theme = "Dark";
    private string _ollamaEndpoint = "http://localhost:11434";
    private string _aiModel = "phi3";
    private string? _wishlistPath;
    private bool _isSaving;

    public string Title => "Settings";

    public string[] AvailableLanguages { get; } = { "en", "es", "fr", "de", "it", "pt-br", "ja", "ko", "zh-cht" };
    public string[] AvailableThemes { get; } = { "Dark", "Light" };
    public string[] AvailableModels { get; } = { "phi3", "llama2", "mistral", "gemma" };

    public string Language
    {
        get => _language;
        set => this.RaiseAndSetIfChanged(ref _language, value);
    }

    public string Theme
    {
        get => _theme;
        set => this.RaiseAndSetIfChanged(ref _theme, value);
    }

    public string OllamaEndpoint
    {
        get => _ollamaEndpoint;
        set => this.RaiseAndSetIfChanged(ref _ollamaEndpoint, value);
    }

    public string AiModel
    {
        get => _aiModel;
        set => this.RaiseAndSetIfChanged(ref _aiModel, value);
    }

    public string? WishlistPath
    {
        get => _wishlistPath;
        set => this.RaiseAndSetIfChanged(ref _wishlistPath, value);
    }

    public bool IsSaving
    {
        get => _isSaving;
        set => this.RaiseAndSetIfChanged(ref _isSaving, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> ReloadManifestCommand { get; }

    // Design-time constructor
    public SettingsViewModel()
    {
        _settingsService = null!;
        SaveCommand = null!;
        ReloadManifestCommand = null!;
    }

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        
        // Load current settings
        var current = _settingsService.CurrentSettings;
        _language = current.Language;
        _theme = current.Theme;
        _ollamaEndpoint = current.OllamaEndpoint;
        _aiModel = current.AiModel;
        _wishlistPath = current.WishlistPath;

        SaveCommand = ReactiveCommand.CreateFromTask(SaveSettingsAsync);
        ReloadManifestCommand = ReactiveCommand.CreateFromTask(ReloadManifestAsync);
    }

    private async Task SaveSettingsAsync()
    {
        IsSaving = true;

        try
        {
            await _settingsService.UpdateSettingAsync(nameof(UserSettings.Language), Language);
            await _settingsService.UpdateSettingAsync(nameof(UserSettings.Theme), Theme);
            await _settingsService.UpdateSettingAsync(nameof(UserSettings.OllamaEndpoint), OllamaEndpoint);
            await _settingsService.UpdateSettingAsync(nameof(UserSettings.AiModel), AiModel);
            await _settingsService.UpdateSettingAsync(nameof(UserSettings.WishlistPath), WishlistPath);

            // Apply theme change immediately
            ApplyTheme();
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void ApplyTheme()
    {
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = Theme == "Light" 
                ? ThemeVariant.Light 
                : ThemeVariant.Dark;
        }
    }

    private async Task ReloadManifestAsync()
    {
        // TODO: Trigger manifest reload with new language
        System.Console.WriteLine($"Reloading manifest for language: {Language}");
        await Task.Delay(100); // Placeholder
    }
}
