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
