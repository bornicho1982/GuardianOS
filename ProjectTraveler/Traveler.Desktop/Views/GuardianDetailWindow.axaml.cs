using Avalonia.Controls;
using Avalonia.Interactivity;
using Traveler.Desktop.ViewModels;
using System.Diagnostics;

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

    /// <summary>
    /// Opens the 3D Guardian Viewer in the default browser
    /// </summary>
    private void OpenInBrowser_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GuardianDetailViewModel vm)
        {
            try
            {
                // Open URL in default browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = vm.Target3DUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GuardianDetail] Failed to open browser: {ex.Message}");
            }
        }
    }
}
