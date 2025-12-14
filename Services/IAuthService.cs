using GuardianOS.Models;

namespace GuardianOS.Services;

/// <summary>
/// Interfaz para el servicio de autenticación OAuth 2.0 con Bungie.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Evento disparado cuando el estado de autenticación cambia.
    /// </summary>
    event EventHandler<bool>? AuthenticationStateChanged;
    
    /// <summary>
    /// Indica si el usuario está actualmente autenticado.
    /// </summary>
    bool IsAuthenticated { get; }
    
    /// <summary>
    /// Token actual de autenticación (null si no autenticado).
    /// </summary>
    AuthToken? CurrentToken { get; }
    
    /// <summary>
    /// Inicia el flujo de autenticación OAuth.
    /// Abre el navegador para que el usuario inicie sesión en Bungie.
    /// </summary>
    /// <returns>True si la autenticación fue exitosa.</returns>
    Task<bool> StartAuthenticationAsync();
    
    /// <summary>
    /// Refresca el token de acceso usando el refresh token.
    /// </summary>
    /// <returns>True si el refresh fue exitoso.</returns>
    Task<bool> RefreshTokenAsync();
    
    /// <summary>
    /// Obtiene un token de acceso válido, refrescando si es necesario.
    /// </summary>
    /// <returns>Token de acceso válido o null si no autenticado.</returns>
    Task<string?> GetValidAccessTokenAsync();
    
    /// <summary>
    /// Cierra la sesión del usuario y elimina los tokens almacenados.
    /// </summary>
    Task LogoutAsync();
    
    /// <summary>
    /// Intenta restaurar la sesión anterior desde el almacenamiento.
    /// </summary>
    /// <returns>True si se pudo restaurar la sesión.</returns>
    Task<bool> TryRestoreSessionAsync();
}
