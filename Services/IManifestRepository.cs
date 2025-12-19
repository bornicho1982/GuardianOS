using GuardianOS.Models;

namespace GuardianOS.Services;

public interface IManifestRepository
{
    Task<InventoryItemDefinition?> GetItemDefinitionAsync(uint hash);

    /// <summary>
    /// Recupera la definición de colores de un Shader (custom dyes).
    /// </summary>
    Task<Models.ShaderDefinition?> GetShaderDefinitionAsync(uint hash);
}
