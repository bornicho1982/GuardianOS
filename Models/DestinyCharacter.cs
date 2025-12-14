using Newtonsoft.Json;

namespace GuardianOS.Models;

/// <summary>
/// Representa un personaje de Destiny 2.
/// </summary>
public class DestinyCharacter
{
    /// <summary>
    /// ID único del personaje.
    /// </summary>
    [JsonProperty("characterId")]
    public string CharacterId { get; set; } = string.Empty;
    
    /// <summary>
    /// Tipo de membresía del dueño.
    /// </summary>
    [JsonProperty("membershipType")]
    public int MembershipType { get; set; }
    
    /// <summary>
    /// ID de membresía del dueño.
    /// </summary>
    [JsonProperty("membershipId")]
    public string MembershipId { get; set; } = string.Empty;
    
    /// <summary>
    /// Nivel de luz actual.
    /// </summary>
    [JsonProperty("light")]
    public int Light { get; set; }
    
    /// <summary>
    /// Tipo de clase (0=Titan, 1=Hunter, 2=Warlock).
    /// </summary>
    [JsonProperty("classType")]
    public int ClassType { get; set; }
    
    /// <summary>
    /// Tipo de raza (0=Human, 1=Awoken, 2=Exo).
    /// </summary>
    [JsonProperty("raceType")]
    public int RaceType { get; set; }
    
    /// <summary>
    /// Tipo de género (0=Male, 1=Female).
    /// </summary>
    [JsonProperty("genderType")]
    public int GenderType { get; set; }
    
    /// <summary>
    /// Hash del emblema actual.
    /// </summary>
    [JsonProperty("emblemHash")]
    public long EmblemHash { get; set; }
    
    /// <summary>
    /// Ruta al icono del emblema.
    /// </summary>
    [JsonProperty("emblemPath")]
    public string EmblemPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Ruta al fondo del emblema.
    /// </summary>
    [JsonProperty("emblemBackgroundPath")]
    public string EmblemBackgroundPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Color del emblema.
    /// </summary>
    [JsonProperty("emblemColor")]
    public EmblemColor? EmblemColor { get; set; }
    
    /// <summary>
    /// Última vez que se jugó con este personaje.
    /// </summary>
    [JsonProperty("dateLastPlayed")]
    public DateTime DateLastPlayed { get; set; }
    
    /// <summary>
    /// Minutos totales jugados.
    /// </summary>
    [JsonProperty("minutesPlayedTotal")]
    public string MinutesPlayedTotal { get; set; } = "0";
    
    /// <summary>
    /// Nivel base del personaje.
    /// </summary>
    [JsonProperty("baseCharacterLevel")]
    public int BaseCharacterLevel { get; set; }
    
    /// <summary>
    /// Estadísticas del personaje (Movilidad, Resistencia, Recuperación, etc.).
    /// Las claves son los hashes de las stats.
    /// </summary>
    [JsonProperty("stats")]
    public Dictionary<string, int> RawStats { get; set; } = new();
    
    // Hashes de estadísticas de Destiny 2
    private const uint STAT_MOBILITY = 2996146975;
    private const uint STAT_RESILIENCE = 392767087;
    private const uint STAT_RECOVERY = 1943323491;
    private const uint STAT_DISCIPLINE = 1735777505;
    private const uint STAT_INTELLECT = 144602215;
    private const uint STAT_STRENGTH = 4244567218;

    // Propiedades de acceso directo a stats
    public int Mobility => RawStats.TryGetValue(STAT_MOBILITY.ToString(), out var v) ? v : 0;
    public int Resilience => RawStats.TryGetValue(STAT_RESILIENCE.ToString(), out var v) ? v : 0;
    public int Recovery => RawStats.TryGetValue(STAT_RECOVERY.ToString(), out var v) ? v : 0;
    public int Discipline => RawStats.TryGetValue(STAT_DISCIPLINE.ToString(), out var v) ? v : 0;
    public int Intellect => RawStats.TryGetValue(STAT_INTELLECT.ToString(), out var v) ? v : 0;
    public int Strength => RawStats.TryGetValue(STAT_STRENGTH.ToString(), out var v) ? v : 0;
    
    // === Propiedades calculadas ===
    
    /// <summary>
    /// Nombre de la clase en español.
    /// </summary>
    public string ClassName => ClassType switch
    {
        0 => "Titán",
        1 => "Cazador",
        2 => "Hechicero",
        _ => "Desconocido"
    };
    
    /// <summary>
    /// Nombre de la raza en español.
    /// </summary>
    public string RaceName => RaceType switch
    {
        0 => "Humano",
        1 => "Insomne",
        2 => "Exo",
        _ => "Desconocido"
    };
    
    /// <summary>
    /// Nombre del género.
    /// </summary>
    public string GenderName => GenderType switch
    {
        0 => "Masculino",
        1 => "Femenino",
        _ => "Desconocido"
    };
    
    /// <summary>
    /// URL completa del icono del emblema.
    /// </summary>
    public string EmblemUrl => !string.IsNullOrEmpty(EmblemPath) 
        ? $"https://www.bungie.net{EmblemPath}" 
        : string.Empty;
    
    /// <summary>
    /// URL completa del fondo del emblema.
    /// </summary>
    public string EmblemBackgroundUrl => !string.IsNullOrEmpty(EmblemBackgroundPath) 
        ? $"https://www.bungie.net{EmblemBackgroundPath}" 
        : string.Empty;
    
    /// <summary>
    /// Descripción corta del personaje.
    /// </summary>
    public string ShortDescription => $"{ClassName} {RaceName} - {Light}✦";
    
    /// <summary>
    /// Horas totales jugadas.
    /// </summary>
    public int HoursPlayed => int.TryParse(MinutesPlayedTotal, out var mins) ? mins / 60 : 0;
    
    /// <summary>
    /// Tipo de daño de la subclase equipada (1=Kinetic, 2=Arc, 3=Solar, 4=Void, 6=Stasis, 7=Strand).
    /// Se establece después de cargar el equipo.
    /// </summary>
    public int SubclassType { get; set; } = 0;
    
    /// <summary>
    /// Color hexadecimal de la subclase para la barra visual.
    /// Colores oficiales de Bungie API.
    /// </summary>
    public string SubclassColor => SubclassType switch
    {
        2 => "#337CFF",   // Arc - Azul (Bungie official)
        3 => "#E55729",   // Solar - Naranja/Rojo (Bungie official)
        4 => "#7D3FFF",   // Void - Morado (Bungie official)
        6 => "#4DDCFF",   // Stasis - Cyan (Bungie official)
        7 => "#3FE069",   // Strand - Verde (Bungie official)
        8 => "#FF40EC",   // Prismatic - Rosa/Magenta (community approximation)
        _ => "#444444"    // Default - Gris oscuro
    };
}

/// <summary>
/// Color RGBA del emblema.
/// </summary>
public class EmblemColor
{
    [JsonProperty("red")]
    public int Red { get; set; }
    
    [JsonProperty("green")]
    public int Green { get; set; }
    
    [JsonProperty("blue")]
    public int Blue { get; set; }
    
    [JsonProperty("alpha")]
    public int Alpha { get; set; }
}
