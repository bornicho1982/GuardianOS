using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Diagnostics;

namespace GuardianOS.Services
{
    /// <summary>
    /// Bridge for communicating with Unity HDRP Viewer via Named Pipes
    /// </summary>
    public class UnityViewerBridge : IDisposable
    {
        private const string PIPE_NAME = "GuardianOS_Viewer";
        private const string UNITY_EXE_PATH = @"UnityViewer\Build\UnityViewer.exe";

        private NamedPipeClientStream? pipeClient;
        private StreamReader? pipeReader;
        private StreamWriter? pipeWriter;
        private Process? unityProcess;
        private CancellationTokenSource? cancellationSource;
        private bool isConnected = false;
        private bool isDisposed = false;

        public event Action<bool>? OnConnectionChanged;
        public event Action<string>? OnEventReceived;
        public event Action<string>? OnError;

        /// <summary>
        /// Start Unity viewer and connect via Named Pipes
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
                        Arguments = "-popupwindow", // Borderless window
                        UseShellExecute = false,
                        CreateNoWindow = false
                    }
                };
                unityProcess.Start();

                // Wait a bit for Unity to initialize
                await Task.Delay(2000);

                // Connect to pipe
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
        public async Task<bool> ConnectAsync(int timeoutMs = 10000)
        {
            try
            {
                pipeClient = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.InOut);
                
                await pipeClient.ConnectAsync(timeoutMs);
                
                pipeReader = new StreamReader(pipeClient);
                pipeWriter = new StreamWriter(pipeClient) { AutoFlush = true };
                isConnected = true;

                OnConnectionChanged?.Invoke(true);

                // Start listening for events
                cancellationSource = new CancellationTokenSource();
                _ = ListenForEventsAsync(cancellationSource.Token);

                return true;
            }
            catch (TimeoutException)
            {
                OnError?.Invoke("Connection timeout - is Unity viewer running?");
                return false;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Connection failed: {ex.Message}");
                return false;
            }
        }

        private async Task ListenForEventsAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && pipeReader != null)
                {
                    string? message = await pipeReader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(message))
                    {
                        OnEventReceived?.Invoke(message);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    OnError?.Invoke($"Listen error: {ex.Message}");
                    isConnected = false;
                    OnConnectionChanged?.Invoke(false);
                }
            }
        }

        /// <summary>
        /// Send command to Unity viewer
        /// </summary>
        public async Task<bool> SendCommandAsync(object command)
        {
            if (!isConnected || pipeWriter == null)
            {
                OnError?.Invoke("Not connected to Unity viewer");
                return false;
            }

            try
            {
                string json = JsonSerializer.Serialize(command);
                await pipeWriter.WriteLineAsync(json);
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Send failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Load a 3D model
        /// </summary>
        public Task<bool> LoadModelAsync(string modelPath)
        {
            return SendCommandAsync(new { action = "loadModel", path = modelPath });
        }

        /// <summary>
        /// Set dye colors for a slot
        /// </summary>
        public Task<bool> SetDyesAsync(int slot, float[] primary, float[]? secondary = null, float[]? tertiary = null)
        {
            var command = new
            {
                action = "setDyes",
                slot = slot,
                primary = primary,
                secondary = secondary,
                tertiary = tertiary
            };
            return SendCommandAsync(command);
        }

        /// <summary>
        /// Set camera position
        /// </summary>
        public Task<bool> SetCameraAsync(float distance, float height)
        {
            return SendCommandAsync(new { action = "setCamera", distance = distance, height = height });
        }

        /// <summary>
        /// Rotate model
        /// </summary>
        public Task<bool> RotateAsync(float angle)
        {
            return SendCommandAsync(new { action = "rotate", angle = angle });
        }

        /// <summary>
        /// Take screenshot
        /// </summary>
        public Task<bool> ScreenshotAsync(string savePath)
        {
            return SendCommandAsync(new { action = "screenshot", path = savePath });
        }

        /// <summary>
        /// Ping to check connection
        /// </summary>
        public Task<bool> PingAsync()
        {
            return SendCommandAsync(new { action = "ping" });
        }

        public void Disconnect()
        {
            cancellationSource?.Cancel();
            pipeClient?.Dispose();
            pipeReader?.Dispose();
            pipeWriter?.Dispose();
            isConnected = false;
            OnConnectionChanged?.Invoke(false);
        }

        public void StopUnity()
        {
            Disconnect();
            if (unityProcess != null && !unityProcess.HasExited)
            {
                unityProcess.Kill();
                unityProcess.Dispose();
            }
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            StopUnity();
        }
    }
}
