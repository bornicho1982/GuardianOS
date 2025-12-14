namespace GuardianOS.Services;

public interface IManifestService
{
    /// <summary>
    /// Verifica si hay una actualización del manifiesto y la descarga si es necesario.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Ruta local al archivo de base de datos SQLite del manifiesto.
    /// </summary>
    string ManifestDatabasePath { get; }

    /// <summary>
    /// Indica si el manifiesto está listo para ser consultado.
    /// </summary>
    bool IsManifestReady { get; }
}
