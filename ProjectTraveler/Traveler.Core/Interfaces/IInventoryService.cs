using System;
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
    /// Fetches the latest inventory data from the Bungie API, hydrates it with Manifest data,
    /// and updates the AllItems collection.
    /// </summary>
    Task RefreshInventoryAsync();
}
