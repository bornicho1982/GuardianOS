using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Main API for controlling the Guardian 3D Viewer from external applications (WPF).
/// Uses TCP sockets for reliable IPC communication.
/// </summary>
public class ViewerAPI : MonoBehaviour
{
    public static ViewerAPI Instance { get; private set; }

    [Header("References")]
    public CharacterLoader characterLoader;
    public DyeController dyeController;
    public CameraController cameraController;

    [Header("IPC Settings")]
    public int port = 12345;
    public bool autoConnect = true;

    private TcpListener tcpListener;
    private TcpClient tcpClient;
    private StreamReader reader;
    private StreamWriter writer;
    private CancellationTokenSource cancellationSource;
    private bool isConnected = false;
    private bool isShuttingDown = false;

    public event Action<string> OnCommandReceived;
    public event Action<bool> OnConnectionChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (autoConnect)
        {
            StartServer();
        }
    }

    private void OnDestroy()
    {
        StopServer();
    }

    private void OnApplicationQuit()
    {
        isShuttingDown = true;
        StopServer();
    }

    public void StartServer()
    {
        cancellationSource = new CancellationTokenSource();
        Task.Run(() => ServerLoop(cancellationSource.Token));
        Debug.Log($"[ViewerAPI] TCP server started on port: {port}");
    }

    public void StopServer()
    {
        try
        {
            isConnected = false;
            cancellationSource?.Cancel();
            
            reader?.Close();
            writer?.Close();
            tcpClient?.Close();
            tcpListener?.Stop();
            
            Debug.Log("[ViewerAPI] Server stopped");
        }
        catch (Exception ex)
        {
            Debug.Log($"[ViewerAPI] Stop error: {ex.Message}");
        }
    }

    private async Task ServerLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && !isShuttingDown)
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Loopback, port);
                tcpListener.Start();
                
                Debug.Log("[ViewerAPI] Waiting for client connection...");
                
                // Use polling to check for pending connections (allows cancellation)
                while (!tcpListener.Pending())
                {
                    if (token.IsCancellationRequested || isShuttingDown)
                    {
                        tcpListener.Stop();
                        return;
                    }
                    await Task.Delay(100, token);
                }
                
                tcpClient = await tcpListener.AcceptTcpClientAsync();
                
                isConnected = true;
                Debug.Log("[ViewerAPI] Client connected!");

                var stream = tcpClient.GetStream();
                reader = new StreamReader(stream);
                writer = new StreamWriter(stream) { AutoFlush = true };

                SendEvent("ready", "1.0");

                while (tcpClient.Connected && !token.IsCancellationRequested && !isShuttingDown)
                {
                    if (stream.DataAvailable)
                    {
                        string message = await reader.ReadLineAsync();
                        if (!string.IsNullOrEmpty(message))
                        {
                            string msg = message;
                            UnityMainThread.Execute(() => ProcessCommand(msg));
                        }
                    }
                    else
                    {
                        await Task.Delay(50, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!isShuttingDown)
                {
                    Debug.LogError($"[ViewerAPI] Server error: {ex.Message}");
                }
            }
            finally
            {
                isConnected = false;
                tcpClient?.Close();
                tcpListener?.Stop();
            }

            if (!isShuttingDown)
            {
                await Task.Delay(1000, token);
            }
        }
    }

    private void ProcessCommand(string json)
    {
        try
        {
            Debug.Log($"[ViewerAPI] Received: {json}");
            OnCommandReceived?.Invoke(json);
            
            if (json.Contains("\"loadModel\""))
            {
                int pathStart = json.IndexOf("\"path\":\"") + 8;
                int pathEnd = json.IndexOf("\"", pathStart);
                string path = json.Substring(pathStart, pathEnd - pathStart);
                characterLoader?.LoadModel(path, (success) => {
                    SendEvent("modelLoaded", success.ToString());
                });
            }
            else if (json.Contains("\"rotate\""))
            {
                int angleStart = json.IndexOf("\"angle\":") + 8;
                int angleEnd = json.IndexOf("}", angleStart);
                if (float.TryParse(json.Substring(angleStart, angleEnd - angleStart), out float angle))
                {
                    cameraController?.RotateBy(angle);
                }
            }
            else if (json.Contains("\"ping\""))
            {
                SendEvent("pong", DateTime.UtcNow.ToString("o"));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ViewerAPI] Command error: {ex.Message}");
            SendEvent("error", ex.Message);
        }
    }

    public void SendEvent(string eventName, string data)
    {
        if (!isConnected || writer == null) return;

        try
        {
            string json = $"{{\"event\":\"{eventName}\",\"data\":\"{data}\"}}";
            writer.WriteLine(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ViewerAPI] Send error: {ex.Message}");
        }
    }
}

/// <summary>
/// Helper class to execute actions on Unity main thread
/// </summary>
public static class UnityMainThread
{
    private static SynchronizationContext mainContext;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        mainContext = SynchronizationContext.Current;
    }
    
    public static void Execute(Action action)
    {
        if (mainContext != null)
        {
            mainContext.Post(_ => action(), null);
        }
        else
        {
            action();
        }
    }
}
