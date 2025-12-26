using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Traveler.Core.Models;

namespace Traveler.Core.Interfaces;

public interface IInventoryService
{
    ObservableCollection<InventoryItem> AllItems { get; }
    
    /// <summary>
    /// Fetches the latest inventory data from the Bungie API, hydrates it with Manifest data,
    /// and updates the AllItems collection.
    /// </summary>
    Task RefreshInventoryAsync();
}
