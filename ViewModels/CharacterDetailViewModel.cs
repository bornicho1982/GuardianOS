using CommunityToolkit.Mvvm.ComponentModel;
using GuardianOS.Models;

namespace GuardianOS.ViewModels;

/// <summary>
/// ViewModel para la vista de detalles del personaje (Inventario/Equipamiento).
/// </summary>
public partial class CharacterDetailViewModel : ViewModelBase
{
    [ObservableProperty]
    private DestinyCharacter _character;

    public CharacterDetailViewModel(DestinyCharacter character)
    {
        _character = character;
    }

    public override Task InitializeAsync()
    {
        // Aquí cargaremos el inventario más adelante
        return Task.CompletedTask;
    }
}
