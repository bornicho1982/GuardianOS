using System.Windows;
using GuardianOS.ViewModels;

namespace GuardianOS.Views;

/// <summary>
/// Code-behind para la ventana principal de GuardianOS.
/// Sigue el patrón MVVM, delegando la lógica al MainViewModel.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Constructor que recibe el ViewModel inyectado.
    /// </summary>
    /// <param name="viewModel">MainViewModel desde el contenedor de DI.</param>
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        
        // Establecer el DataContext para binding MVVM
        DataContext = viewModel;
        
        // Inicializar el ViewModel cuando la ventana se carga
        Loaded += OnWindowLoaded;
    }
    
    /// <summary>
    /// Maneja el evento de carga de la ventana.
    /// Inicia la verificación de conexión con Bungie.
    /// </summary>
    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}
