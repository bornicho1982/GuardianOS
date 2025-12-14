namespace GuardianOS.Models;

/// <summary>
/// Representa la respuesta del endpoint /Destiny2/Manifest/
/// Contiene información sobre las definiciones del juego y rutas de descarga.
/// </summary>
public class DestinyManifest
{
    /// <summary>
    /// Versión actual del Manifest.
    /// </summary>
    public string? Version { get; set; }
    
    /// <summary>
    /// Ruta al archivo de contenido móvil (mundo, assets).
    /// </summary>
    public string? MobileAssetContentPath { get; set; }
    
    /// <summary>
    /// Rutas a las bases de datos de contenido del mundo por idioma.
    /// Key: código de idioma (ej: "en", "es"), Value: ruta del archivo.
    /// </summary>
    public Dictionary<string, string>? MobileWorldContentPaths { get; set; }
    
    /// <summary>
    /// Rutas a las bases de datos JSON por idioma.
    /// </summary>
    public Dictionary<string, string>? JsonWorldContentPaths { get; set; }
    
    /// <summary>
    /// Ruta a la base de datos de contenido de mundo comprimida.
    /// </summary>
    public string? MobileWorldContentPathsCompressed { get; set; }
    
    /// <summary>
    /// Definiciones de componentes JSON por entidad y idioma.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>>? JsonWorldComponentContentPaths { get; set; }
    
    /// <summary>
    /// Ruta al archivo de Gear Asset Database.
    /// </summary>
    public string? MobileGearAssetDataBases { get; set; }
    
    /// <summary>
    /// Información de caché del Gear CDN.
    /// </summary>
    public List<GearAssetDataBase>? MobileGearCDN { get; set; }
    
    /// <summary>
    /// Icono que representa la versión actual del Manifest.
    /// </summary>
    public string? IconImagePyramidInfo { get; set; }
}

/// <summary>
/// Representa una base de datos de assets de equipo.
/// </summary>
public class GearAssetDataBase
{
    /// <summary>
    /// Versión del asset database.
    /// </summary>
    public int Version { get; set; }
    
    /// <summary>
    /// Ruta al archivo de la base de datos.
    /// </summary>
    public string? Path { get; set; }
}

/// <summary>
/// Wrapper genérico para las respuestas de la API de Bungie.
/// Todas las respuestas de la API siguen este formato.
/// </summary>
/// <typeparam name="T">Tipo del objeto Response.</typeparam>
public class BungieApiResponse<T>
{
    /// <summary>
    /// Código de error de la API. 1 = Success.
    /// </summary>
    public int ErrorCode { get; set; }
    
    /// <summary>
    /// Código de estado del error (si hay error).
    /// </summary>
    public int ThrottleSeconds { get; set; }
    
    /// <summary>
    /// Estado de la respuesta ("Success", "Error", etc.).
    /// </summary>
    public string? ErrorStatus { get; set; }
    
    /// <summary>
    /// Mensaje descriptivo del error (si lo hay).
    /// </summary>
    public string? Message { get; set; }
    
    /// <summary>
    /// Detalles adicionales del mensaje.
    /// </summary>
    public Dictionary<string, string>? MessageData { get; set; }
    
    /// <summary>
    /// El objeto de respuesta principal que contiene los datos solicitados.
    /// </summary>
    public T? Response { get; set; }
    
    /// <summary>
    /// Indica si la respuesta fue exitosa.
    /// </summary>
    public bool IsSuccess => ErrorCode == 1;
}
