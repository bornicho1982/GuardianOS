using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GuardianOS.Models;
using GuardianOS.Services;
using CommunityToolkit.Mvvm.Messaging;
using GuardianOS.Messages;

namespace GuardianOS.ViewModels;

/// <summary>
/// ViewModel para el Dashboard principal (vista de usuario autenticado).
/// Gestiona la visualización de personajes, divisas y rotaciones semanales.
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
    private readonly IBungieApiService _bungieApiService;
    private readonly IAuthService _authService;
    private readonly string _membershipId;
    private readonly int _membershipType;

    #region Observable Properties

    /// <summary>
    /// Lista de personajes del jugador.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DestinyCharacter> _characters = new();

    /// <summary>
    /// Personaje actualmente seleccionado.
    /// </summary>
    [ObservableProperty]
    private DestinyCharacter? _selectedCharacter;

    /// <summary>
    /// Indica si se están cargando los personajes.
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingCharacters;

    /// <summary>
    /// Cantidad de Glimmer.
    /// </summary>
    [ObservableProperty]
    private int _glimmer;

    /// <summary>
    /// Cantidad de Bright Dust.
    /// </summary>
    [ObservableProperty]
    private int _brightDust;

    /// <summary>
    /// Cantidad de Enhancement Cores (Prisms visualmente en UI actual).
    /// </summary>
    [ObservableProperty]
    private int _enhancementCores;

    /// <summary>
    /// Mensaje de estado local del dashboard.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "Cargando datos...";

    #endregion

    /// <summary>
    /// Constructor.
    /// </summary>
    public DashboardViewModel(IBungieApiService bungieApiService, IAuthService authService, string membershipId, int membershipType)
    {
        _bungieApiService = bungieApiService;
        _authService = authService;
        _membershipId = membershipId;
        _membershipType = membershipType;
    }

    /// <summary>
    /// Inicializa los datos del Dashboard.
    /// </summary>
    public override async Task InitializeAsync()
    {
        await LoadCharactersAsync();
    }

    private async Task LoadCharactersAsync()
    {
        try
        {
            IsLoadingCharacters = true;
            StatusMessage = "Obteniendo datos de personajes...";

            var token = await _authService.GetValidAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                StatusMessage = "Error: Token no válido";
                IsLoadingCharacters = false;
                return;
            }

            // Obtener datos del perfil con personajes
            var profileData = await _bungieApiService.GetProfileAsync(
                _membershipType, 
                _membershipId, 
                token);

            if (profileData?.Characters?.Data == null)
            {
                StatusMessage = "No se encontraron personajes";
                return;
            }

            // Agregar personajes a la colección
            Characters.Clear();
            foreach (var character in profileData.Characters.Data.Values.OrderByDescending(c => c.DateLastPlayed))
            {
                // Extraer tipo de subclase del equipo
                if (profileData.CharacterEquipment?.Data != null &&
                    profileData.CharacterEquipment.Data.TryGetValue(character.CharacterId, out var equipment))
                {
                    // Bucket hash 3284755031 = Subclass slot
                    var subclassItem = equipment.Items?.FirstOrDefault(i => i.BucketHash == 3284755031);
                    if (subclassItem != null)
                    {
                        character.SubclassType = GetDamageTypeFromSubclassHash(subclassItem.ItemHash);
                    }
                }
                Characters.Add(character);
            }

            // Seleccionar el último jugado por defecto
            SelectedCharacter = Characters.FirstOrDefault();

            // Extraer currencies del perfil
            if (profileData.ProfileCurrencies?.Data?.Items != null)
            {
                const long GLIMMER_HASH = 3159615086;
                const long BRIGHT_DUST_HASH = 2817410917;
                const long ENHANCEMENT_PRISMS_HASH = 3036656991;

                foreach (var currency in profileData.ProfileCurrencies.Data.Items)
                {
                    switch (currency.ItemHash)
                    {
                        case GLIMMER_HASH:
                            Glimmer = currency.Quantity;
                            break;
                        case BRIGHT_DUST_HASH:
                            BrightDust = currency.Quantity;
                            break;
                        case ENHANCEMENT_PRISMS_HASH:
                            EnhancementCores = currency.Quantity;
                            break;
                    }
                }
            }

            StatusMessage = $"{Characters.Count} personaje(s) cargados";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al cargar: {ex.Message}";
            Debug.WriteLine($"[Dashboard] LoadCharacters error: {ex.Message}");
        }
        finally
        {
            IsLoadingCharacters = false;
        }
    }

    [RelayCommand]
    private void SelectCharacter(DestinyCharacter? character)
    {
        if (character == null) return;
        SelectedCharacter = character;
        
        // Navegar a la vista de detalles
        WeakReferenceMessenger.Default.Send(new CharacterSelectedMessage(character));
    }

    /// <summary>
    /// Helper para mapear hash de subclase a tipo de daño.
    /// </summary>
    private static int GetDamageTypeFromSubclassHash(long itemHash)
    {
        return itemHash switch
        {
            // Solar
            2240888816 or 2550323932 or 3941205951 => 3,
            // Arc
            2328211300 or 2932390016 or 1616346845 or 3168997075 => 2,
            // Void
            2453351420 or 2842471112 or 2849050827 => 4,
            // Stasis
            873720784 or 613647804 or 3291545503 => 6,
            // Strand
            3785442599 or 242419885 or 4204413574 => 7,
            // Prismatic
            _ when IsPrismaticHash(itemHash) => 8,
            _ => 0
        };
    }

    private static bool IsPrismaticHash(long itemHash)
    {
        return itemHash is 4282591831 or 2806466524 or 3979749617 or 1946006466;
    }
}
