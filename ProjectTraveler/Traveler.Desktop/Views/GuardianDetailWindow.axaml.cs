using Avalonia.Controls;
using Traveler.Desktop.ViewModels;

namespace Traveler.Desktop.Views;

public partial class GuardianDetailWindow : Window
{
    public GuardianDetailWindow()
    {
        InitializeComponent();
    }

    public GuardianDetailWindow(GuardianDetailViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
