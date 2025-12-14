using GuardianOS.Models;

namespace GuardianOS.Services;

/// <summary>
/// Interfaz que define el contrato para comunicación con la API de Bungie.
/// Implementa el patrón de inyección de dependencias para facilitar testing y mantenibilidad.
/// </summary>
public interface IBungieApiService
{
    #region Manifest Operations
    
    /// <summary>
    /// Obtiene la información del Manifest de Destiny 2.
    /// El Manifest contiene la base de datos completa de definiciones del juego.
    /// </summary>
    /// <returns>Objeto DestinyManifest con las rutas de descarga.</returns>
    Task<DestinyManifest?> GetDestinyManifestAsync();
    
    #endregion
    
    #region Health Check
    
    /// <summary>
    /// Verifica la conectividad con la API de Bungie.
    /// Útil para comprobar que la API Key es válida y el servicio está disponible.
    /// </summary>
    /// <returns>True si la conexión es exitosa, False en caso contrario.</returns>
    Task<bool> TestConnectionAsync();
    
    #endregion
    
    #region Authenticated Operations
    
    /// <summary>
    /// Obtiene los perfiles vinculados a una cuenta de Bungie.
    /// Requiere token de autenticación.
    /// </summary>
    /// <param name="membershipId">ID de membresía de Bungie.net</param>
    /// <param name="accessToken">Token OAuth</param>
    Task<LinkedProfilesResponse?> GetLinkedProfilesAsync(string membershipId, string accessToken);
    
    /// <summary>
    /// Obtiene información del perfil del usuario (personajes, inventario, etc).
    /// </summary>
    Task<DestinyProfileResponse?> GetProfileAsync(int membershipType, string membershipId, string accessToken, int[]? components = null);
    
    #endregion
}
