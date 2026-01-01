using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Traveler.Core.Models;

namespace Traveler.Core.Interfaces;

public interface IInventoryService
{
    ObservableCollection<InventoryItem> AllItems { get; }
    
    /// <summary>
    /// List of all characters for the current account.
    /// </summary>
    ObservableCollection<CharacterInfo> Characters { get; }
    
    /// <summary>
    /// Event fired when inventory refresh is complete.
    /// </summary>
    event Action? InventoryRefreshed;
    
    /// <summary>
    /// Fetches the latest inventory data from the Bungie API.
    /// </summary>
    Task RefreshInventoryAsync();
    
    /// <summary>
    /// Transfers an item between locations.
    /// </summary>
    Task TransferItemAsync(InventoryItem item, string targetCharacterId, bool toVault, int stackSize = 1);
    
    /// <summary>
    /// Gets characters for the current authenticated user.
    /// </summary>
    Task<List<CharacterInfo>> GetCurrentUserCharactersAsync();
    
    /// <summary>
    /// Gets the current vault space usage.
    /// </summary>
    Task<(int used, int total)> GetVaultStatusAsync();
    
    /// <summary>
    /// Gets the postmaster item count for a character.
    /// </summary>
    Task<int> GetPostmasterCountAsync(string characterId);
    
    /// <summary>
    /// Gets profile currencies.
    /// </summary>
    Task<Dictionary<uint, long>> GetCurrenciesAsync();
}

