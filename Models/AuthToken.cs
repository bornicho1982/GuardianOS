namespace GuardianOS.Models;

/// <summary>
/// Representa los tokens de autenticación OAuth 2.0 de Bungie.
/// </summary>
public class AuthToken
{
    /// <summary>
    /// Token de acceso para hacer peticiones a la API.
    /// Tiene una duración de 1 hora.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;
    
    /// <summary>
    /// Token para renovar el acceso sin re-autenticar.
    /// Tiene una duración de 90 días.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
    
    /// <summary>
    /// Fecha/hora de expiración del AccessToken.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// Fecha/hora de expiración del RefreshToken.
    /// </summary>
    public DateTime RefreshExpiresAt { get; set; }
    
    /// <summary>
    /// ID de membresía de Bungie del usuario.
    /// </summary>
    public string MembershipId { get; set; } = string.Empty;
    
    /// <summary>
    /// Indica si el AccessToken ha expirado.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    
    /// <summary>
    /// Indica si el RefreshToken ha expirado.
    /// </summary>
    public bool IsRefreshExpired => DateTime.UtcNow >= RefreshExpiresAt;
    
    /// <summary>
    /// Indica si el token necesita ser refrescado (5 minutos antes de expirar).
    /// </summary>
    public bool NeedsRefresh => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5);
}

/// <summary>
/// Respuesta del endpoint de token de Bungie.
/// </summary>
public class BungieTokenResponse
{
    public string? access_token { get; set; }
    public string? token_type { get; set; }
    public int expires_in { get; set; }
    public string? refresh_token { get; set; }
    public int refresh_expires_in { get; set; }
    public string? membership_id { get; set; }
}
