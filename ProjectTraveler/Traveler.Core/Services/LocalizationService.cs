using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Traveler.Core.Services;

/// <summary>
/// Service for handling application localization (Spanish/English).
/// </summary>
public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private string _currentLanguage = "es";
    
    public event PropertyChangedEventHandler? PropertyChanged;

    // Language dictionaries
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new()
    {
        ["es"] = new Dictionary<string, string>
        {
            // Navigation
            ["nav.home"] = "Inicio",
            ["nav.inventory"] = "Inventario",
            ["nav.loadouts"] = "Equipamientos",
            ["nav.triumphs"] = "Triunfos",
            ["nav.vendors"] = "Vendedores",
            ["nav.settings"] = "Configuración",
            
            // Dashboard
            ["dashboard.title"] = "Centro de Comando",
            ["dashboard.guardians"] = "TUS GUARDIANES",
            ["dashboard.quickActions"] = "ACCIONES RÁPIDAS",
            ["dashboard.notifications"] = "NOTIFICACIONES",
            ["dashboard.noNotifications"] = "Sin notificaciones nuevas",
            
            // Guardian Status
            ["status.powerLevel"] = "NIVEL DE PODER",
            ["status.maxAchievable"] = "MÁX ALCANZABLE",
            ["status.artifact"] = "Artefacto",
            ["status.currencies"] = "DIVISAS",
            ["status.glimmer"] = "Luminosidad",
            ["status.legendaryShards"] = "Fragmentos Legendarios",
            ["status.brightDust"] = "Polvo Brillante",
            ["status.stats"] = "ESTADÍSTICAS",
            ["status.vaultItems"] = "Ítems en Depósito",
            ["status.postmaster"] = "Administrador Postal",
            ["status.exotics"] = "Exóticos",
            ["status.refresh"] = "Actualizar Todo",
            
            // Inventory
            ["inv.weapons"] = "ARMAS",
            ["inv.armor"] = "ARMADURA",
            ["inv.kinetic"] = "CINÉTICA",
            ["inv.energy"] = "ENERGÍA",
            ["inv.power"] = "PESADA",
            ["inv.helmet"] = "CASCO",
            ["inv.gauntlets"] = "BRAZOS",
            ["inv.chest"] = "PECHO",
            ["inv.legs"] = "BOTAS",
            ["inv.classItem"] = "DISTINTIVO",
            ["inv.vault"] = "DEPÓSITO",
            ["inv.search"] = "Buscar...",
            
            // Common
            ["common.cancel"] = "Cancelar",
            ["common.confirm"] = "Confirmar",
            ["common.save"] = "Guardar",
            ["common.loading"] = "Cargando...",
            ["common.error"] = "Error",
            ["common.success"] = "Éxito",
        },
        ["en"] = new Dictionary<string, string>
        {
            // Navigation
            ["nav.home"] = "Home",
            ["nav.inventory"] = "Inventory",
            ["nav.loadouts"] = "Loadouts",
            ["nav.triumphs"] = "Triumphs",
            ["nav.vendors"] = "Vendors",
            ["nav.settings"] = "Settings",
            
            // Dashboard
            ["dashboard.title"] = "Command Center",
            ["dashboard.guardians"] = "YOUR GUARDIANS",
            ["dashboard.quickActions"] = "QUICK ACTIONS",
            ["dashboard.notifications"] = "NOTIFICATIONS",
            ["dashboard.noNotifications"] = "No new notifications",
            
            // Guardian Status
            ["status.powerLevel"] = "POWER LEVEL",
            ["status.maxAchievable"] = "MAX ACHIEVABLE",
            ["status.artifact"] = "Artifact",
            ["status.currencies"] = "CURRENCIES",
            ["status.glimmer"] = "Glimmer",
            ["status.legendaryShards"] = "Legendary Shards",
            ["status.brightDust"] = "Bright Dust",
            ["status.stats"] = "STATISTICS",
            ["status.vaultItems"] = "Vault Items",
            ["status.postmaster"] = "Postmaster",
            ["status.exotics"] = "Exotics",
            ["status.refresh"] = "Refresh All",
            
            // Inventory
            ["inv.weapons"] = "WEAPONS",
            ["inv.armor"] = "ARMOR",
            ["inv.kinetic"] = "KINETIC",
            ["inv.energy"] = "ENERGY",
            ["inv.power"] = "POWER",
            ["inv.helmet"] = "HELMET",
            ["inv.gauntlets"] = "GAUNTLETS",
            ["inv.chest"] = "CHEST",
            ["inv.legs"] = "LEGS",
            ["inv.classItem"] = "CLASS ITEM",
            ["inv.vault"] = "VAULT",
            ["inv.search"] = "Search...",
            
            // Common
            ["common.cancel"] = "Cancel",
            ["common.confirm"] = "Confirm",
            ["common.save"] = "Save",
            ["common.loading"] = "Loading...",
            ["common.error"] = "Error",
            ["common.success"] = "Success",
        }
    };

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value && _translations.ContainsKey(value))
            {
                _currentLanguage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSpanish));
                OnPropertyChanged(nameof(IsEnglish));
                LanguageChanged?.Invoke(this, value);
            }
        }
    }

    public bool IsSpanish => _currentLanguage == "es";
    public bool IsEnglish => _currentLanguage == "en";

    public event EventHandler<string>? LanguageChanged;

    /// <summary>
    /// Gets a translated string by key.
    /// </summary>
    public string this[string key] => Get(key);

    /// <summary>
    /// Gets a translated string by key.
    /// </summary>
    public string Get(string key)
    {
        if (_translations.TryGetValue(_currentLanguage, out var dict) && 
            dict.TryGetValue(key, out var value))
        {
            return value;
        }
        
        // Fallback to English
        if (_translations.TryGetValue("en", out var enDict) && 
            enDict.TryGetValue(key, out var enValue))
        {
            return enValue;
        }
        
        return $"[{key}]";
    }

    /// <summary>
    /// Sets the language to Spanish.
    /// </summary>
    public void SetSpanish() => CurrentLanguage = "es";

    /// <summary>
    /// Sets the language to English.
    /// </summary>
    public void SetEnglish() => CurrentLanguage = "en";

    /// <summary>
    /// Toggles between Spanish and English.
    /// </summary>
    public void ToggleLanguage()
    {
        CurrentLanguage = _currentLanguage == "es" ? "en" : "es";
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
