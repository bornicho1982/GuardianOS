using GuardianOS.Models;

namespace GuardianOS.Services;

public interface IManifestRepository
{
    /// <summary>
    /// Busca la definición de un ítem por su Hash.
    /// </summary>
    Task<InventoryItemDefinition?> GetItemDefinitionAsync(uint hash);
}
