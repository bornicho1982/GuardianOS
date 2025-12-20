using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace GuardianOS.Services
{
    /// <summary>
    /// Bridge for communicating with Unity HDRP Viewer via TCP sockets
    /// </summary>
    public class UnityViewerBridge : IDisposable
    {
        private const int PORT = 12345;
        private const string UNITY_EXE_PATH = @"UnityViewer\Build\GuardianOS Viewer.exe";

        private TcpClient? tcpClient;
        private StreamReader? reader;
        private StreamWriter? writer;
        private Process? unityProcess;
        private CancellationTokenSource? cancellationSource;
        private bool isConnected = false;
        private bool isDisposed = false;

        public event Action<bool>? OnConnectionChanged;
        public event Action<string>? OnEventReceived;
        public event Action<string>? OnError;

        /// <summary>
        /// Start Unity viewer and connect via TCP
        /// </summary>
        public async Task<bool> StartAndConnectAsync(string? unityExePath = null)
        {
            try
            {
                string exePath = unityExePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, UNITY_EXE_PATH);

                if (!File.Exists(exePath))
                {
                    OnError?.Invoke($"Unity viewer not found: {exePath}");
                    return false;
                }

                // Start Unity process
                unityProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "-screen-width 800 -screen-height 600 -screen-fullscreen 0",
                        UseShellExecute = false,
                        CreateNoWindow = false
                    }
                };
                unityProcess.Start();

                // Wait for Unity to initialize
                await Task.Delay(3000);

                // Connect via TCP
                return await ConnectAsync();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to start Unity: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Connect to already running Unity viewer
        /// </summary>
        public async Task<bool> ConnectAsync(int maxRetries = 10)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync("127.0.0.1", PORT);
                    
                    var stream = tcpClient.GetStream();
                    reader = new StreamReader(stream);
                    writer = new StreamWriter(stream) { AutoFlush = true };
                    isConnected = true;

                    OnConnectionChanged?.Invoke(true);

                    // Start listening for events
                    cancellationSource = new CancellationTokenSource();
                    _ = ListenForEventsAsync(cancellationSource.Token);

                    return true;
                }
                catch (SocketException)
                {
                    // Server not ready yet, retry
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Connection error: {ex.Message}");
                    return false;
                }
            }
            
            OnError?.Invoke("Connection timeout - Unity viewer not responding");
            return false;
        }

        private async Task ListenForEventsAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && isConnected && reader != null)
                {
                    if (tcpClient?.GetStream().DataAvailable == true)
                    {
                        string? line = await reader.ReadLineAsync();
                        if (!string.IsNullOrEmpty(line))
                        {
                            OnEventReceived?.Invoke(line);
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
                // Normal cancellation
            }
            catch (Exception ex)
            {
                if (!isDisposed)
                {
                    OnError?.Invoke($"Listen error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Send a command to Unity
        /// </summary>
        public async Task SendCommandAsync(string action, object? parameters = null)
        {
            if (!isConnected || writer == null) return;

            try
            {
                string json = parameters != null
                    ? $"{{\"{action}\":{System.Text.Json.JsonSerializer.Serialize(parameters)}}}"
                    : $"{{\"{action}\":true}}";
                
                await writer.WriteLineAsync(json);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Send error: {ex.Message}");
            }
        }

        public Task LoadModelAsync(string path) =>
            SendCommandAsync("loadModel", new { path });

        public Task SetDyesAsync(string primary, string secondary, string tertiary) =>
            SendCommandAsync("setDyes", new { primary, secondary, tertiary });

        public Task SetCameraAsync(float azimuth, float elevation, float distance) =>
            SendCommandAsync("setCamera", new { azimuth, elevation, distance });

        public Task RotateAsync(float angle) =>
            SendCommandAsync("rotate", new { angle });

        public Task PingAsync() =>
            SendCommandAsync("ping");

        public void Disconnect()
        {
            try
            {
                isConnected = false;
                cancellationSource?.Cancel();
                
                reader?.Close();
                writer?.Close();
                tcpClient?.Close();
                
                OnConnectionChanged?.Invoke(false);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Disconnect error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            Disconnect();

            try
            {
                if (unityProcess != null && !unityProcess.HasExited)
                {
                    unityProcess.Kill();
                    unityProcess.Dispose();
                }
            }
            catch { }
        }
    }
}
