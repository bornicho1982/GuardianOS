using CommunityToolkit.Mvvm.ComponentModel;

namespace GuardianOS.ViewModels;

/// <summary>
/// Clase base para todos los ViewModels de la aplicación.
/// Hereda de ObservableObject del CommunityToolkit.Mvvm para
/// notificaciones automáticas de cambios en propiedades.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    private bool _isBusy;
    private string? _title;
    
    /// <summary>
    /// Indica si el ViewModel está procesando una operación.
    /// Útil para mostrar indicadores de carga en la UI.
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }
    
    /// <summary>
    /// Título de la vista actual.
    /// </summary>
    public string? Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
    
    /// <summary>
    /// Método virtual para inicialización asíncrona.
    /// Los ViewModels derivados pueden sobrescribir este método
    /// para cargar datos cuando la vista se muestra.
    /// </summary>
    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Método virtual para limpieza cuando la vista se cierra.
    /// </summary>
    public virtual void Cleanup()
    {
    }
}
