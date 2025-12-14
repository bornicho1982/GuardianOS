using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GuardianOS.Messages;
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
        Character = character;
    }

    [RelayCommand]
    private void GoBack()
    {
        WeakReferenceMessenger.Default.Send(new NavigateToDashboardMessage());
    }

    public override Task InitializeAsync()
    {
        // Aquí cargaremos el inventario más adelante
        return Task.CompletedTask;
    }
}
