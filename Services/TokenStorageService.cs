using System.IO;
using System.Security.Cryptography;
using System.Text;
using GuardianOS.Models;
using Newtonsoft.Json;

namespace GuardianOS.Services;

/// <summary>
/// Servicio para almacenar tokens de forma segura usando Windows DPAPI.
/// Los tokens se encriptan antes de guardarse en disco.
/// </summary>
public class TokenStorageService
{
    private readonly string _tokenFilePath;
    
    /// <summary>
    /// Constructor que inicializa la ruta del archivo de tokens.
    /// </summary>
    public TokenStorageService()
    {
        // Guardar en AppData/Local/GuardianOS
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var guardianPath = Path.Combine(appDataPath, "GuardianOS");
        
        // Crear directorio si no existe
        if (!Directory.Exists(guardianPath))
        {
            Directory.CreateDirectory(guardianPath);
        }
        
        _tokenFilePath = Path.Combine(guardianPath, "auth.dat");
    }
    
    /// <summary>
    /// Guarda el token de forma encriptada.
    /// </summary>
    /// <param name="token">Token a guardar.</param>
    public async Task SaveTokenAsync(AuthToken token)
    {
        try
        {
            // Serializar a JSON
            var json = JsonConvert.SerializeObject(token);
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            
            // Encriptar usando DPAPI (solo descifrable en esta máquina/usuario)
            var encryptedBytes = ProtectedData.Protect(
                jsonBytes, 
                null, 
                DataProtectionScope.CurrentUser);
            
            // Guardar en archivo
            await File.WriteAllBytesAsync(_tokenFilePath, encryptedBytes);
            
            System.Diagnostics.Debug.WriteLine("[TokenStorage] Token guardado de forma segura");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TokenStorage] Error al guardar: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Carga el token desde el almacenamiento encriptado.
    /// </summary>
    /// <returns>Token almacenado o null si no existe.</returns>
    public async Task<AuthToken?> LoadTokenAsync()
    {
        try
        {
            if (!File.Exists(_tokenFilePath))
            {
                return null;
            }
            
            // Leer archivo encriptado
            var encryptedBytes = await File.ReadAllBytesAsync(_tokenFilePath);
            
            // Desencriptar usando DPAPI
            var jsonBytes = ProtectedData.Unprotect(
                encryptedBytes, 
                null, 
                DataProtectionScope.CurrentUser);
            
            // Deserializar
            var json = Encoding.UTF8.GetString(jsonBytes);
            var token = JsonConvert.DeserializeObject<AuthToken>(json);
            
            System.Diagnostics.Debug.WriteLine("[TokenStorage] Token cargado correctamente");
            return token;
        }
        catch (CryptographicException)
        {
            // El archivo está corrupto o fue creado por otro usuario
            System.Diagnostics.Debug.WriteLine("[TokenStorage] Token corrupto, eliminando...");
            DeleteToken();
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TokenStorage] Error al cargar: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Elimina el token almacenado.
    /// </summary>
    public void DeleteToken()
    {
        try
        {
            if (File.Exists(_tokenFilePath))
            {
                File.Delete(_tokenFilePath);
                System.Diagnostics.Debug.WriteLine("[TokenStorage] Token eliminado");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TokenStorage] Error al eliminar: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Verifica si existe un token almacenado.
    /// </summary>
    public bool HasStoredToken => File.Exists(_tokenFilePath);
}
