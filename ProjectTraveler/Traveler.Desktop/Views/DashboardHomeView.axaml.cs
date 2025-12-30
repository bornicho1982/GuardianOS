using Avalonia.Controls;
using Avalonia.Input;
using Traveler.Core.Models;
using Traveler.Desktop.ViewModels;

namespace Traveler.Desktop.Views;

public partial class DashboardHomeView : UserControl
{
    public DashboardHomeView()
    {
        InitializeComponent();
    }
    
    private void OnCharacterCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && 
            border.DataContext is CharacterInfo character &&
            DataContext is DashboardHomeViewModel vm)
        {
            vm.SelectCharacterCommand.Execute(character).Subscribe();
        }
    }

    private void GuardianCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && 
            border.Tag is CharacterInfo character &&
            DataContext is DashboardHomeViewModel vm)
        {
            vm.OpenGuardianDetailCommand.Execute(character).Subscribe();
        }
    }
}
