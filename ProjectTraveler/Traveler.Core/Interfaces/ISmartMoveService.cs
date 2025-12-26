using System.Threading.Tasks;
using Traveler.Core.Models;

namespace Traveler.Core.Interfaces;

public interface ISmartMoveService
{
    /// <summary>
    /// intelligently moves an item to the target character or vault.
    /// If the target is full, it will attempt to move the oldest item to the vault first.
    /// </summary>
    /// <param name="item">The item to move.</param>
    /// <param name="targetId">The CharacterId of the target, or "Vault".</param>
    Task TransferItemAsync(InventoryItem item, string targetId);
}
