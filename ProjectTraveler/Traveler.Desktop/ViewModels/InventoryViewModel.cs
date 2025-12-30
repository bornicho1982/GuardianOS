using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using ReactiveUI;
using Traveler.Core.Interfaces;
using Traveler.Core.Models;

namespace Traveler.Desktop.ViewModels;

public class InventoryViewModel : ViewModelBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ISmartMoveService _smartMoveService;
    
    private string _searchText = "";
    private CharacterInfo? _selectedCharacter;
    private string _selectedBucketFilter = "All";

    public ObservableCollection<InventoryItem> Items => _inventoryService.AllItems;
    public ObservableCollection<CharacterInfo> Characters => _inventoryService.Characters;
    
    // === SELECTED CHARACTER ===
    public CharacterInfo? SelectedCharacter
    {
        get => _selectedCharacter;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedCharacter, value);
            OnCharacterChanged();
        }
    }
    
    // === SELECTED BUCKET FILTER (For sync scroll) ===
    public string SelectedBucketFilter
    {
        get => _selectedBucketFilter;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedBucketFilter, value);
            this.RaisePropertyChanged(nameof(FilteredVaultItems));
            this.RaisePropertyChanged(nameof(VaultItemCount));
            // Notify all bucket filters for active state
            this.RaisePropertyChanged(nameof(IsKineticActive));
            this.RaisePropertyChanged(nameof(IsEnergyActive));
            this.RaisePropertyChanged(nameof(IsPowerActive));
            this.RaisePropertyChanged(nameof(IsHelmetActive));
            this.RaisePropertyChanged(nameof(IsGauntletsActive));
            this.RaisePropertyChanged(nameof(IsChestActive));
            this.RaisePropertyChanged(nameof(IsLegsActive));
            this.RaisePropertyChanged(nameof(IsClassItemActive));
        }
    }
    
    // === BUCKET FILTER ACTIVE STATES ===
    public bool IsKineticActive => SelectedBucketFilter == "Kinetic Weapons";
    public bool IsEnergyActive => SelectedBucketFilter == "Energy Weapons";
    public bool IsPowerActive => SelectedBucketFilter == "Power Weapons";
    public bool IsHelmetActive => SelectedBucketFilter == "Helmet";
    public bool IsGauntletsActive => SelectedBucketFilter == "Gauntlets";
    public bool IsChestActive => SelectedBucketFilter == "Chest Armor";
    public bool IsLegsActive => SelectedBucketFilter == "Leg Armor";
    public bool IsClassItemActive => SelectedBucketFilter == "Class Armor";
    
    // === SEARCH ===
    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            OnInventoryRefreshed();
        }
    }
    
    private IEnumerable<InventoryItem> FilterItems(IEnumerable<InventoryItem> items)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return items;
            
        var search = SearchText.ToLower();
        return items.Where(i => 
            i.Name.ToLower().Contains(search) ||
            i.ItemType.ToLower().Contains(search) ||
            i.TierType.ToLower().Contains(search));
    }
    
    // === EQUIPPED ITEMS (1 per slot) ===
    public InventoryItem? EquippedKinetic => GetEquipped("Kinetic Weapons");
    public InventoryItem? EquippedEnergy => GetEquipped("Energy Weapons");
    public InventoryItem? EquippedPower => GetEquipped("Power Weapons");
    public InventoryItem? EquippedHelmet => GetEquipped("Helmet");
    public InventoryItem? EquippedGauntlets => GetEquipped("Gauntlets");
    public InventoryItem? EquippedChest => GetEquipped("Chest Armor");
    public InventoryItem? EquippedLegs => GetEquipped("Leg Armor");
    public InventoryItem? EquippedClassItem => GetEquipped("Class Armor");
    
    private InventoryItem? GetEquipped(string bucketType)
    {
        if (SelectedCharacter == null) return null;
        return Items.FirstOrDefault(i => 
            i.Location == SelectedCharacter.CharacterId.ToString() &&
            i.BucketType == bucketType &&
            i.IsEquipped);
    }
    
    // === INVENTORY ITEMS (non-equipped, 9 slots max) ===
    public IEnumerable<InventoryItem> InventoryKinetic => GetInventory("Kinetic Weapons");
    public IEnumerable<InventoryItem> InventoryEnergy => GetInventory("Energy Weapons");
    public IEnumerable<InventoryItem> InventoryPower => GetInventory("Power Weapons");
    public IEnumerable<InventoryItem> InventoryHelmet => GetInventory("Helmet");
    public IEnumerable<InventoryItem> InventoryGauntlets => GetInventory("Gauntlets");
    public IEnumerable<InventoryItem> InventoryChest => GetInventory("Chest Armor");
    public IEnumerable<InventoryItem> InventoryLegs => GetInventory("Leg Armor");
    public IEnumerable<InventoryItem> InventoryClassItem => GetInventory("Class Armor");
    
    private IEnumerable<InventoryItem> GetInventory(string bucketType)
    {
        if (SelectedCharacter == null) return Enumerable.Empty<InventoryItem>();
        return FilterItems(Items.Where(i => 
            i.Location == SelectedCharacter.CharacterId.ToString() &&
            i.BucketType == bucketType &&
            !i.IsEquipped)).Take(9);
    }
    
    // === FILTERED VAULT ITEMS (based on SelectedBucketFilter) ===
    public IEnumerable<InventoryItem> FilteredVaultItems
    {
        get
        {
            if (SelectedCharacter == null) return Enumerable.Empty<InventoryItem>();
            
            var charClass = SelectedCharacter.ClassName;
            var vaultItems = Items.Where(i => i.Location == "vault");
            
            // Apply bucket filter
            if (SelectedBucketFilter != "All")
            {
                vaultItems = vaultItems.Where(i => i.BucketType == SelectedBucketFilter);
            }
            
            // Filter armor by class (weapons work for all classes)
            // Only filter if class type is specified and doesn't match
            var armorBuckets = new[] { "Helmet", "Gauntlets", "Chest Armor", "Leg Armor", "Class Armor" };
            vaultItems = vaultItems.Where(i => 
            {
                // If not armor, allow all (weapons)
                if (!armorBuckets.Contains(i.BucketType))
                    return true;
                    
                // For armor: if ClassType is empty/Any, allow it
                if (string.IsNullOrEmpty(i.ClassType) || i.ClassType == "Any")
                    return true;
                    
                // Otherwise, must match character class
                return i.ClassType == charClass;
            });
            
            return FilterItems(vaultItems);
        }
    }
    
    // === VAULT ITEM COUNT ===
    public int VaultItemCount => FilteredVaultItems.Count();
    
    // === CHARACTER SELECTION HELPER ===
    public bool IsCharacterSelected(CharacterInfo? character)
    {
        if (character == null || SelectedCharacter == null) return false;
        return character.CharacterId == SelectedCharacter.CharacterId;
    }

    // === COMMANDS ===
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RefreshCommand { get; }
    public ReactiveCommand<CharacterInfo, System.Reactive.Unit> SelectCharacterCommand { get; }
    public ReactiveCommand<string, System.Reactive.Unit> SetBucketFilterCommand { get; }

    public InventoryViewModel(IInventoryService inventoryService, ISmartMoveService smartMoveService)
    {
        _inventoryService = inventoryService;
        _smartMoveService = smartMoveService;

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshInventory);
        SelectCharacterCommand = ReactiveCommand.Create<CharacterInfo>(SelectCharacter);
        SetBucketFilterCommand = ReactiveCommand.Create<string>(SetBucketFilter);
        
        _inventoryService.InventoryRefreshed += OnInventoryRefreshed;
        
        RxApp.MainThreadScheduler.Schedule(async () => await RefreshInventory());
    }
    
    private void SelectCharacter(CharacterInfo character)
    {
        SelectedCharacter = character;
    }
    
    private void SetBucketFilter(string bucketType)
    {
        SelectedBucketFilter = bucketType;
    }
    
    private void OnCharacterChanged()
    {
        // Notify all equipped items
        this.RaisePropertyChanged(nameof(EquippedKinetic));
        this.RaisePropertyChanged(nameof(EquippedEnergy));
        this.RaisePropertyChanged(nameof(EquippedPower));
        this.RaisePropertyChanged(nameof(EquippedHelmet));
        this.RaisePropertyChanged(nameof(EquippedGauntlets));
        this.RaisePropertyChanged(nameof(EquippedChest));
        this.RaisePropertyChanged(nameof(EquippedLegs));
        this.RaisePropertyChanged(nameof(EquippedClassItem));
        
        // Notify all inventory items
        this.RaisePropertyChanged(nameof(InventoryKinetic));
        this.RaisePropertyChanged(nameof(InventoryEnergy));
        this.RaisePropertyChanged(nameof(InventoryPower));
        this.RaisePropertyChanged(nameof(InventoryHelmet));
        this.RaisePropertyChanged(nameof(InventoryGauntlets));
        this.RaisePropertyChanged(nameof(InventoryChest));
        this.RaisePropertyChanged(nameof(InventoryLegs));
        this.RaisePropertyChanged(nameof(InventoryClassItem));
        
        // Notify vault
        this.RaisePropertyChanged(nameof(FilteredVaultItems));
    }

    private void OnInventoryRefreshed()
    {
        if (SelectedCharacter == null && Characters.Any())
        {
            SelectedCharacter = Characters.First();
        }
        
        this.RaisePropertyChanged(nameof(Characters));
        OnCharacterChanged();
    }

    public InventoryViewModel() 
    {
        _inventoryService = null!;
        _smartMoveService = null!;
        RefreshCommand = null!;
        SelectCharacterCommand = null!;
        SetBucketFilterCommand = null!;
    }

    private async Task RefreshInventory()
    {
        await _inventoryService.RefreshInventoryAsync();
    }
}
