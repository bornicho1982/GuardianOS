using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace GuardianOS.Services
{
    /// <summary>
    /// Service to generate 3D Collada models from Destiny 2 item hashes
    /// using the Destiny-Collada-Generator tool
    /// </summary>
    public class ColladaGeneratorService
    {
        private readonly string _dcgPath;
        private readonly string _outputPath;
        private readonly string _cachePath;

        public ColladaGeneratorService()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            _dcgPath = Path.Combine(basePath, "Tools", "ColladaGenerator", "DestinyColladaGenerator-v1-7-15.exe");
            _outputPath = Path.Combine(basePath, "Assets", "3DViewer", "models");
            _cachePath = Path.Combine(basePath, "cache", "collada");

            // Ensure directories exist
            Directory.CreateDirectory(_outputPath);
            Directory.CreateDirectory(_cachePath);
        }

        /// <summary>
        /// Generate a Collada (.dae) model for the given item hash
        /// </summary>
        public async Task<string?> GenerateModel(uint itemHash)
        {
            var cachedPath = Path.Combine(_cachePath, $"{itemHash}.dae");
            
            // Check cache first
            if (File.Exists(cachedPath))
            {
                return cachedPath;
            }

            if (!File.Exists(_dcgPath))
            {
                Console.WriteLine($"[ColladaGenerator] DCG not found at: {_dcgPath}");
                return null;
            }

            try
            {
                var tempOutput = Path.Combine(_cachePath, $"temp_{itemHash}");
                Directory.CreateDirectory(tempOutput);

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _dcgPath,
                        Arguments = $"-i {itemHash} -o \"{tempOutput}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(_dcgPath)
                    }
                };

                Console.WriteLine($"[ColladaGenerator] Generating model for item {itemHash}...");
                process.Start();
                
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    // Find the generated .dae file
                    var daeFiles = Directory.GetFiles(tempOutput, "*.dae", SearchOption.AllDirectories);
                    if (daeFiles.Length > 0)
                    {
                        File.Copy(daeFiles[0], cachedPath, true);
                        Console.WriteLine($"[ColladaGenerator] Model generated: {cachedPath}");
                        
                        // Clean up temp
                        try { Directory.Delete(tempOutput, true); } catch { }
                        
                        return cachedPath;
                    }
                }
                else
                {
                    Console.WriteLine($"[ColladaGenerator] Error: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ColladaGenerator] Exception: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Generate models for multiple item hashes
        /// </summary>
        public async Task<string[]> GenerateModels(uint[] itemHashes)
        {
            var results = new List<string>();
            
            foreach (var hash in itemHashes)
            {
                var path = await GenerateModel(hash);
                if (path != null)
                {
                    results.Add(path);
                }
            }

            return results.ToArray();
        }

        /// <summary>
        /// Check if DCG tool is available
        /// </summary>
        public bool IsAvailable => File.Exists(_dcgPath);
    }
}
