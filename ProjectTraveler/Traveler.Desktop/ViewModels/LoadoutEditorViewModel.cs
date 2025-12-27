using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Traveler.Core.Interfaces;
using Traveler.Core.Models;
using Traveler.Core.Models.Optimization;
using Traveler.Core.Optimization;

namespace Traveler.Desktop.ViewModels;

/// <summary>
/// ViewModel for creating and editing loadouts with optimization constraints.
/// </summary>
public class LoadoutEditorViewModel : ViewModelBase
{
    private readonly OptimizationSolver _solver;
    private readonly IBuildCopilotService _buildCopilotService;
    private readonly IInventoryService _inventoryService;

    private string _loadoutName = "New Loadout";
    private string _naturalLanguageQuery = "";
    private bool _isProcessing;
    private string _aiResponse = "";
    private InventoryItem? _lockedExotic;

    // Armor 3.0: Stats are 0-200
    private int _minMobility;
    private int _minResilience;
    private int _minRecovery;
    private int _minDiscipline;
    private int _minIntellect;
    private int _minStrength;

    public string LoadoutName
    {
        get => _loadoutName;
        set => this.RaiseAndSetIfChanged(ref _loadoutName, value);
    }

    public string NaturalLanguageQuery
    {
        get => _naturalLanguageQuery;
        set => this.RaiseAndSetIfChanged(ref _naturalLanguageQuery, value);
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set => this.RaiseAndSetIfChanged(ref _isProcessing, value);
    }

    public string AiResponse
    {
        get => _aiResponse;
        set => this.RaiseAndSetIfChanged(ref _aiResponse, value);
    }

    public InventoryItem? LockedExotic
    {
        get => _lockedExotic;
        set => this.RaiseAndSetIfChanged(ref _lockedExotic, value);
    }

    // Stat constraints (0-200 scale for Armor 3.0)
    public int MinMobility { get => _minMobility; set => this.RaiseAndSetIfChanged(ref _minMobility, value); }
    public int MinResilience { get => _minResilience; set => this.RaiseAndSetIfChanged(ref _minResilience, value); }
    public int MinRecovery { get => _minRecovery; set => this.RaiseAndSetIfChanged(ref _minRecovery, value); }
    public int MinDiscipline { get => _minDiscipline; set => this.RaiseAndSetIfChanged(ref _minDiscipline, value); }
    public int MinIntellect { get => _minIntellect; set => this.RaiseAndSetIfChanged(ref _minIntellect, value); }
    public int MinStrength { get => _minStrength; set => this.RaiseAndSetIfChanged(ref _minStrength, value); }

    /// <summary>
    /// Available exotic armor pieces for locking.
    /// </summary>
    public ObservableCollection<InventoryItem> AvailableExotics { get; } = new();

    /// <summary>
    /// Result of the last optimization.
    /// </summary>
    public ObservableCollection<InventoryItem> OptimizedItems { get; } = new();

    /// <summary>
    /// Tuning recommendations for Tier 5 pieces.
    /// </summary>
    public ObservableCollection<string> TuningRecommendations { get; } = new();

    // Commands
    public ReactiveCommand<Unit, Unit> RunOptimizerCommand { get; }
    public ReactiveCommand<Unit, Unit> AskAiCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveLoadoutCommand { get; }

    // Design-time constructor
    public LoadoutEditorViewModel()
    {
        _solver = null!;
        _buildCopilotService = null!;
        _inventoryService = null!;
        RunOptimizerCommand = null!;
        AskAiCommand = null!;
        SaveLoadoutCommand = null!;
    }

    public LoadoutEditorViewModel(
        OptimizationSolver solver,
        IBuildCopilotService buildCopilotService,
        IInventoryService inventoryService)
    {
        _solver = solver;
        _buildCopilotService = buildCopilotService;
        _inventoryService = inventoryService;

        // Populate available exotics
        foreach (var item in _inventoryService.AllItems.Where(i => i.IsExotic && IsArmor(i.ItemType)))
        {
            AvailableExotics.Add(item);
        }

        // Commands
        var canOptimize = this.WhenAnyValue(x => x.IsProcessing, p => !p);
        RunOptimizerCommand = ReactiveCommand.CreateFromTask(RunOptimizerAsync, canOptimize);
        AskAiCommand = ReactiveCommand.CreateFromTask(AskAiAsync, canOptimize);
        SaveLoadoutCommand = ReactiveCommand.Create(SaveLoadout);
    }

    private async Task RunOptimizerAsync()
    {
        IsProcessing = true;
        OptimizedItems.Clear();
        TuningRecommendations.Clear();

        try
        {
            // Build request from UI constraints
            var request = new LoadoutRequest
            {
                AvailableItems = _inventoryService.AllItems
                    .Where(i => IsArmor(i.ItemType))
                    .ToList(),
                MinimumStats = new Dictionary<uint, int>
                {
                    { 2996146975, MinMobility },   // Mobility
                    { 392767087, MinResilience },  // Resilience
                    { 1943323491, MinRecovery },   // Recovery
                    { 1735777505, MinDiscipline }, // Discipline
                    { 144602215, MinIntellect },   // Intellect
                    { 4244567218, MinStrength }    // Strength
                }
            };

            // If exotic is locked, filter to include only that exotic
            if (LockedExotic != null)
            {
                request.AvailableItems = request.AvailableItems
                    .Where(i => !i.IsExotic || i.InstanceId == LockedExotic.InstanceId)
                    .ToList();
            }

            // Run solver
            var result = _solver.Solve(request);

            if (result.IsValid)
            {
                foreach (var item in result.SelectedItems)
                {
                    OptimizedItems.Add(item);
                }

                foreach (var tuning in result.TuningAdjustments)
                {
                    TuningRecommendations.Add($"Item {tuning.Key}: {tuning.Value}");
                }

                AiResponse = $"Optimization complete! Total stats: {result.FinalStats.Values.Sum()}";
            }
            else
            {
                AiResponse = "No valid loadout found with these constraints. Try relaxing requirements.";
            }
        }
        catch (Exception ex)
        {
            AiResponse = $"Optimization error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task AskAiAsync()
    {
        if (string.IsNullOrWhiteSpace(NaturalLanguageQuery))
            return;

        IsProcessing = true;
        AiResponse = "Thinking...";

        try
        {
            var recommendation = await _buildCopilotService.SuggestBuildAsync(NaturalLanguageQuery);
            
            AiResponse = $"""
                Build: {recommendation.BuildName ?? "Custom Build"}
                Exotic: {recommendation.RecommendedExoticArmor ?? "Any"}
                Weapons: {string.Join(", ", recommendation.RecommendedWeapons)}
                
                {recommendation.Reasoning}
                """;
        }
        catch (Exception ex)
        {
            AiResponse = $"AI Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private void SaveLoadout()
    {
        // TODO: Persist loadout to local storage
        Console.WriteLine($"Saving loadout: {LoadoutName} with {OptimizedItems.Count} items");
    }

    private static bool IsArmor(string itemType)
    {
        return itemType.Contains("Helmet", StringComparison.OrdinalIgnoreCase)
            || itemType.Contains("Gauntlets", StringComparison.OrdinalIgnoreCase)
            || itemType.Contains("Chest", StringComparison.OrdinalIgnoreCase)
            || itemType.Contains("Leg", StringComparison.OrdinalIgnoreCase)
            || itemType.Contains("Class", StringComparison.OrdinalIgnoreCase);
    }
}
