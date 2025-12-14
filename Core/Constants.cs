namespace GuardianOS.Core;

/// <summary>
/// Constantes globales de la aplicación.
/// Contiene URLs base y configuración para la API de Bungie.
/// </summary>
public static class Constants
{
    #region Bungie API Configuration
    
    /// <summary>
    /// API Key proporcionada por Bungie para acceder a su API.
    /// Aplicación: ACATALEPSIA
    /// </summary>
    public const string BUNGIE_API_KEY = "e1a73d9d631a46a8b7e2b6e37ae30492";
    
    /// <summary>
    /// URL base de la API de Bungie.
    /// </summary>
    public const string BUNGIE_API_BASE_URL = "https://www.bungie.net/Platform";
    
    /// <summary>
    /// URL base para recursos estáticos (imágenes, iconos, etc.).
    /// </summary>
    public const string BUNGIE_STATIC_BASE_URL = "https://www.bungie.net";
    
    /// <summary>
    /// URL para el flujo de autorización OAuth 2.0.
    /// Nota: Usamos /es/ para la versión en español, pero funciona igual con /en/
    /// </summary>
    public const string BUNGIE_AUTH_URL = "https://www.bungie.net/es/OAuth/Authorize";
    
    /// <summary>
    /// URL para obtener tokens OAuth 2.0.
    /// </summary>
    public const string BUNGIE_TOKEN_URL = "https://www.bungie.net/Platform/App/OAuth/Token/";
    
    #endregion
    
    #region OAuth Configuration
    
    /// <summary>
    /// Client ID de la aplicación ACATALEPSIA registrada en Bungie.
    /// </summary>
    public const string OAUTH_CLIENT_ID = "50831";
    
    /// <summary>
    /// Client Secret de la aplicación (cliente confidencial).
    /// </summary>
    public const string OAUTH_CLIENT_SECRET = "E7ijog02U.jWK8oRz-rf5wkT.6e4NNrAfoj-eIhVXj8";
    
    /// <summary>
    /// URI de redirección configurada en Bungie (HTTPS requerido).
    /// </summary>
    public const string OAUTH_REDIRECT_URI = "https://localhost:8080/callback";
    
    #endregion
    
    #region Application Settings
    
    /// <summary>
    /// Nombre de la aplicación para User-Agent headers.
    /// </summary>
    public const string APP_NAME = "GuardianOS";
    
    /// <summary>
    /// Versión actual de la aplicación.
    /// </summary>
    public const string APP_VERSION = "1.0.0";
    
    /// <summary>
    /// Ruta local donde se almacena el Manifest SQLite.
    /// </summary>
    public const string MANIFEST_DATABASE_PATH = "Data/Manifest.db";
    
    #endregion
}
