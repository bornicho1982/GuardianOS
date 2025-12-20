using UnityEngine;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Main API for controlling the Guardian 3D Viewer from external applications (WPF).
/// Communicates via Named Pipes for real-time commands.
/// </summary>
public class ViewerAPI : MonoBehaviour
{
    public static ViewerAPI Instance { get; private set; }

    [Header("References")]
    public CharacterLoader characterLoader;
    public DyeController dyeController;
    public CameraController cameraController;

    [Header("IPC Settings")]
    public string pipeName = "GuardianOS_Viewer";
    public bool autoConnect = true;

    private NamedPipeServerStream pipeServer;
    private StreamReader pipeReader;
    private StreamWriter pipeWriter;
    private CancellationTokenSource cancellationSource;
    private bool isConnected = false;

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

    public void StartServer()
    {
        cancellationSource = new CancellationTokenSource();
        Task.Run(() => ServerLoop(cancellationSource.Token));
        Debug.Log($"[ViewerAPI] Named Pipe server started: {pipeName}");
    }

    public void StopServer()
    {
        cancellationSource?.Cancel();
        pipeServer?.Dispose();
        isConnected = false;
    }

    private async Task ServerLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message);
                
                Debug.Log("[ViewerAPI] Waiting for client connection...");
                await pipeServer.WaitForConnectionAsync(token);
                
                isConnected = true;
                Debug.Log("[ViewerAPI] Client connected!");

                pipeReader = new StreamReader(pipeServer);
                pipeWriter = new StreamWriter(pipeServer) { AutoFlush = true };

                SendEvent("ready", "1.0");

                while (pipeServer.IsConnected && !token.IsCancellationRequested)
                {
                    string message = await pipeReader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(message))
                    {
                        // Process on main thread
                        string msg = message;
                        UnityMainThread.Execute(() => ProcessCommand(msg));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ViewerAPI] Pipe error: {ex.Message}");
            }
            finally
            {
                isConnected = false;
                pipeServer?.Dispose();
            }

            await Task.Delay(1000, token);
        }
    }

    private void ProcessCommand(string json)
    {
        try
        {
            Debug.Log($"[ViewerAPI] Received: {json}");
            OnCommandReceived?.Invoke(json);
            
            // Simple JSON parsing without Newtonsoft
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
        if (!isConnected || pipeWriter == null) return;

        try
        {
            string json = $"{{\"event\":\"{eventName}\",\"data\":\"{data}\"}}";
            pipeWriter.WriteLine(json);
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
