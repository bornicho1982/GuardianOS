using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

    /// <summary>
    /// Start the Named Pipe server for IPC communication
    /// </summary>
    public void StartServer()
    {
        cancellationSource = new CancellationTokenSource();
        Task.Run(() => ServerLoop(cancellationSource.Token));
        Debug.Log($"[ViewerAPI] Named Pipe server started: {pipeName}");
    }

    /// <summary>
    /// Stop the Named Pipe server
    /// </summary>
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
                MainThreadDispatcher.Enqueue(() => OnConnectionChanged?.Invoke(true));
                Debug.Log("[ViewerAPI] Client connected!");

                pipeReader = new StreamReader(pipeServer);
                pipeWriter = new StreamWriter(pipeServer) { AutoFlush = true };

                // Send ready message
                SendEvent("ready", new { version = "1.0" });

                while (pipeServer.IsConnected && !token.IsCancellationRequested)
                {
                    string message = await pipeReader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(message))
                    {
                        MainThreadDispatcher.Enqueue(() => ProcessCommand(message));
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
                MainThreadDispatcher.Enqueue(() => OnConnectionChanged?.Invoke(false));
                pipeServer?.Dispose();
            }

            // Wait before retrying
            await Task.Delay(1000, token);
        }
    }

    /// <summary>
    /// Process incoming command from WPF
    /// </summary>
    private void ProcessCommand(string json)
    {
        try
        {
            var command = JObject.Parse(json);
            string action = command["action"]?.ToString();

            Debug.Log($"[ViewerAPI] Received command: {action}");
            OnCommandReceived?.Invoke(json);

            switch (action)
            {
                case "loadModel":
                    string modelPath = command["path"]?.ToString();
                    if (!string.IsNullOrEmpty(modelPath))
                    {
                        characterLoader.LoadModel(modelPath, (success) => {
                            SendEvent("modelLoaded", new { success = success, path = modelPath });
                        });
                    }
                    break;

                case "setDyes":
                    int slot = command["slot"]?.ToObject<int>() ?? 0;
                    var primary = command["primary"]?.ToObject<float[]>();
                    var secondary = command["secondary"]?.ToObject<float[]>();
                    var tertiary = command["tertiary"]?.ToObject<float[]>();
                    
                    if (primary != null)
                        dyeController.SetDye(slot, DyeType.Primary, new Color(primary[0], primary[1], primary[2]));
                    if (secondary != null)
                        dyeController.SetDye(slot, DyeType.Secondary, new Color(secondary[0], secondary[1], secondary[2]));
                    if (tertiary != null)
                        dyeController.SetDye(slot, DyeType.Tertiary, new Color(tertiary[0], tertiary[1], tertiary[2]));
                    
                    SendEvent("dyesApplied", new { slot = slot });
                    break;

                case "setCamera":
                    float distance = command["distance"]?.ToObject<float>() ?? 3f;
                    float height = command["height"]?.ToObject<float>() ?? 1.5f;
                    cameraController.SetPosition(distance, height);
                    break;

                case "rotate":
                    float angle = command["angle"]?.ToObject<float>() ?? 0f;
                    cameraController.RotateBy(angle);
                    break;

                case "screenshot":
                    string savePath = command["path"]?.ToString();
                    TakeScreenshot(savePath);
                    break;

                case "ping":
                    SendEvent("pong", new { timestamp = DateTime.UtcNow.ToString("o") });
                    break;

                default:
                    SendEvent("error", new { message = $"Unknown action: {action}" });
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ViewerAPI] Command error: {ex.Message}");
            SendEvent("error", new { message = ex.Message });
        }
    }

    /// <summary>
    /// Send event back to WPF
    /// </summary>
    public void SendEvent(string eventName, object data)
    {
        if (!isConnected || pipeWriter == null) return;

        try
        {
            var eventObj = new { @event = eventName, data = data };
            string json = JsonConvert.SerializeObject(eventObj);
            pipeWriter.WriteLine(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ViewerAPI] Send error: {ex.Message}");
        }
    }

    private void TakeScreenshot(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            path = Path.Combine(Application.persistentDataPath, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        }
        ScreenCapture.CaptureScreenshot(path);
        SendEvent("screenshotTaken", new { path = path });
    }
}
