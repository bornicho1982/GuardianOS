using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GuardianOS.Models;
using GuardianOS.Services;

namespace GuardianOS.ViewModels;

/// <summary>
/// ViewModel para la gestión y visualización del inventario.
/// </summary>
public partial class InventoryViewModel : ViewModelBase
{
    private readonly IBungieApiService _bungieApiService;
    private readonly IAuthService _authService;
    private readonly IManifestService _manifestService;
    private readonly IManifestRepository _manifestRepository;
    private readonly string _membershipId;
    private readonly int _membershipType;

    /// <summary>
    /// Todos los items del inventario.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<InventoryItem> _inventoryItems = new();

    /// <summary>
    /// Items filtrados para mostrar en la vista actual.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<InventoryItem> _filteredItems = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Esperando carga...";

    public InventoryViewModel(IBungieApiService bungieApiService, IAuthService authService, IManifestService manifestService, IManifestRepository manifestRepository, string membershipId, int membershipType)
    {
        _bungieApiService = bungieApiService;
        _authService = authService;
        _manifestService = manifestService;
        _manifestRepository = manifestRepository;
        _membershipId = membershipId;
        _membershipType = membershipType;
    }

    public override async Task InitializeAsync()
    {
        await LoadInventoryAsync();
    }

    private async Task LoadInventoryAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Cargando inventario...";

            var token = await _authService.GetValidAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                StatusMessage = "Error: No hay token válido.";
                return;
            }
            
            // 102: Vault, 201: CharacterInventories, 205: CharacterEquipment, 300: ItemInstances
            var components = new[] { 102, 201, 205, 300 };
            var profileResponse = await _bungieApiService.GetProfileAsync(_membershipType, _membershipId, token, components);

            if (profileResponse == null)
            {
                StatusMessage = "Error al obtener datos de Bungie.";
                return;
            }

            var allItems = new List<InventoryItem>();
            var itemInstances = profileResponse.ItemComponents?.Instances?.Data;

            // 1. Procesar Inventario de Personajes (Mochila)
            if (profileResponse.CharacterInventories?.Data != null)
            {
                foreach (var (characterId, invData) in profileResponse.CharacterInventories.Data)
                {
                    if (invData.Items != null)
                    {
                        foreach (var item in invData.Items)
                        {
                            await ProcessItem(item, long.Parse(characterId), itemInstances, allItems, false);
                        }
                    }
                }
            }

            // 2. Procesar Equipo de Personajes (Equipado)
            if (profileResponse.CharacterEquipment?.Data != null)
            {
                foreach (var (characterId, equipData) in profileResponse.CharacterEquipment.Data)
                {
                    if (equipData.Items != null)
                    {
                        foreach (var item in equipData.Items)
                        {
                            // Convertir EquippedItem a DestinyItemComponent para reusar lógica
                            var component = new DestinyItemComponent
                            {
                                ItemHash = (uint)item.ItemHash,
                                ItemInstanceId = long.TryParse(item.ItemInstanceId, out var id) ? (long?)id : null,
                                BucketHash = (uint)item.BucketHash,
                                Quantity = 1,
                                Location = 1 // 1 = Character
                            };
                            await ProcessItem(component, long.Parse(characterId), itemInstances, allItems, true);
                        }
                    }
                }
            }

            // 3. Procesar Vault (Depósito)
            if (profileResponse.ProfileInventory?.Data?.Items != null)
            {
                foreach (var item in profileResponse.ProfileInventory.Data.Items)
                {
                    await ProcessItem(item, 0, itemInstances, allItems, false); // 0 = Vault owner
                }
            }

            // Actualizar Colección en UI Thread
            InventoryItems.Clear();
            foreach (var item in allItems)
            {
                InventoryItems.Add(item);
            }

            // Filtrar y mostrar
            FilteredItems.Clear();
            foreach (var item in InventoryItems)
            {
               FilteredItems.Add(item);
            }

            StatusMessage = $"{InventoryItems.Count} items cargados.";
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"LoadInventoryError: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ProcessItem(
        DestinyItemComponent item, 
        long ownerId, 
        Dictionary<string, ItemInstanceData>? instances, 
        List<InventoryItem> targetList,
        bool isEquipped)
    {
        var def = await _manifestRepository.GetItemDefinitionAsync(item.ItemHash);
        if (def == null) return;

        // Crear modelo InventoryItem
        var invItem = new InventoryItem
        {
            ItemHash = item.ItemHash,
            ItemInstanceId = item.ItemInstanceId,
            Quantity = item.Quantity,
            CharacterId = ownerId,
            BucketHash = item.BucketHash,
            State = item.State,
            
            // Datos del Manifiesto
            Name = def.Name,
            Icon = def.Icon,
            Screenshot = def.DisplayProperties.Icon, // Usar Icon como fallback si no hay screenshot específica mapped
            TierType = def.Inventory.TierTypeName,
            ItemTypeDisplayName = def.ItemTypeDisplayName,
            IsEquipped = isEquipped
        };

        // Enriquecer con Instancia si existe
        if (item.ItemInstanceId.HasValue && instances != null && 
            instances.TryGetValue(item.ItemInstanceId.Value.ToString(), out var instData))
        {
            invItem.PrimaryStatValue = instData.PrimaryStat?.Value ?? instData.ItemLevel;
            invItem.DamageType = instData.DamageType;
        }
        else
        {
            // Si no tiene instancia, usar cantidad como valor primario (ej: materiales)
            invItem.PrimaryStatValue = item.Quantity;
        }

        targetList.Add(invItem);
    }
}
