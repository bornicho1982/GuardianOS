using CommunityToolkit.Mvvm.ComponentModel;

namespace GuardianOS.Models;

/// <summary>
/// Representa un item del inventario de Destiny 2.
/// Combina datos de la instancia (API) y de la definición estática (Manifiesto).
/// </summary>
public partial class InventoryItem : ObservableObject
{
    // --- Datos de Instancia (API) ---

    /// <summary>
    /// Hash único del ítem.
    /// </summary>
    [ObservableProperty]
    private long _itemHash;

    /// <summary>
    /// ID de la instancia única (para ítems instanciados como armas/armaduras).
    /// </summary>
    [ObservableProperty]
    private long? _itemInstanceId;

    /// <summary>
    /// Cantidad del ítem (para consumibles/materiales).
    /// </summary>
    [ObservableProperty]
    private int _quantity;
    
    /// <summary>
    /// ID del personaje o ubicación donde está el ítem.
    /// </summary>
    [ObservableProperty]
    private long _characterId;

    /// <summary>
    /// Hash del bucket donde está ubicado (Inventario, Cinética, etc).
    /// </summary>
    [ObservableProperty]
    private long _bucketHash;
    
    /// <summary>
    /// Nivel de poder calculado (Primary Stat).
    /// </summary>
    [ObservableProperty]
    private int _primaryStatValue;

    /// <summary>
    /// Tipo de daño (Solar, Arc, Void, etc) si aplica.
    /// </summary>
    [ObservableProperty]
    private int _damageType;

    /// <summary>
    /// Estado del item (Bloqueado, Trackeado, Masterwork).
    /// Bitmask: 1=None, 2=Locked, 4=Tracked, 8=Masterwork
    /// </summary>
    [ObservableProperty]
    private int _state;


    // --- Datos de Definición (Manifiesto) ---

    /// <summary>
    /// Nombre localizado del ítem.
    /// </summary>
    [ObservableProperty]
    private string? _name;

    /// <summary>
    /// Ruta relativa al icono (ej: /common/destiny2_content/icons/...).
    /// </summary>
    [ObservableProperty]
    private string? _icon;

    /// <summary>
    /// Ruta relativa a la imagen de fondo/screenshot (para tooltips o detalles).
    /// </summary>
    [ObservableProperty]
    private string? _screenshot;

    /// <summary>
    /// Tipo de tier (Exotic, Legendary, Rare, Common).
    /// </summary>
    [ObservableProperty]
    private string? _tierType;

    /// <summary>
    /// Nombre del tipo de ítem (ej: "Cañón de mano").
    /// </summary>
    [ObservableProperty]
    private string? _itemTypeDisplayName;
    
    /// <summary>
    /// Indica si es un ítem equipado.
    /// </summary>
    [ObservableProperty]
    private bool _isEquipped;

    /// <summary>
    /// URL absoluta completa para el icono.
    /// </summary>
    public string IconUrl => !string.IsNullOrEmpty(Icon) 
        ? $"https://www.bungie.net{Icon}" 
        : "/Assets/missing_icon.png"; // Placeholder local si falla

    // --- Helpers de UI ---

    public bool IsMasterwork => (State & 4) == 4; // Check bit 4 for masterwork (value 4? need to verify enum)
    public bool IsLocked => (State & 1) == 1;     // Check bit 1
}
