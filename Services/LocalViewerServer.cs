using System.Net;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace GuardianOS.Services;

/// <summary>
/// A simple static file server to bypass CORS issues when loading local 3D viewer assets.
/// </summary>
public class LocalViewerServer
{
    private HttpListener? _listener;
    private string _rootDirectory;
    private int _port;
    private bool _isRunning;

    public string Url => $"http://localhost:{_port}/";

    public LocalViewerServer(string rootDirectory, int port = 51234)
    {
        _rootDirectory = rootDirectory;
        _port = port;
    }

    public void Start()
    {
        if (_isRunning) return;

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(Url);
            _listener.Start();
            _isRunning = true;
            
            Task.Run(() => ListenLoop());
            Debug.WriteLine($"[LocalServer] Started at {Url} serving {_rootDirectory}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocalServer] Failed to start: {ex.Message}");
            // Try fallback port
            try 
            {
                _port++;
                _listener = new HttpListener();
                _listener.Prefixes.Add(Url);
                _listener.Start();
                _isRunning = true;
                Task.Run(() => ListenLoop());
                Debug.WriteLine($"[LocalServer] Started at {Url} serving {_rootDirectory}");
            }
            catch(Exception retryEx)
            {
                Debug.WriteLine($"[LocalServer] Retry failed: {retryEx.Message}");
            }
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _listener?.Stop();
        _listener?.Close();
    }

    private async Task ListenLoop()
    {
        while (_isRunning && _listener!.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                ProcessRequest(context);
            }
            catch (HttpListenerException)
            {
                // Listener stopped
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalServer] Error in listen loop: {ex.Message}");
            }
        }
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        try
        {
            string filename = context.Request.Url!.LocalPath;
            if (filename == "/") filename = "/viewer.html";
            
            // Remove leading slash and combine
            filename = filename.TrimStart('/');
            string path = Path.Combine(_rootDirectory, filename);

            // Security check: prevent directory traversal
            if (!Path.GetFullPath(path).StartsWith(Path.GetFullPath(_rootDirectory)))
            {
                context.Response.StatusCode = 403;
                context.Response.Close();
                return;
            }

            if (File.Exists(path))
            {
                byte[] buffer = File.ReadAllBytes(path);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.ContentType = GetContentType(Path.GetExtension(path));
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.StatusCode = 200;
            }
            else
            {
                context.Response.StatusCode = 404;
            }
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            Debug.WriteLine($"[LocalServer] Error processing request: {ex.Message}");
        }
        finally
        {
            try { context.Response.Close(); } catch { }
        }
    }

    private string GetContentType(string extension)
    {
        return extension.ToLower() switch
        {
            ".html" => "text/html",
            ".js" => "application/javascript",
            ".css" => "text/css",
            ".json" => "application/json",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".fbx" => "application/octet-stream",
            ".glb" => "model/gltf-binary",
            _ => "application/octet-stream"
        };
    }
}
