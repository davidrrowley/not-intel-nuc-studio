using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace NucLedController.WinUI3.Services
{
    /// <summary>
    /// Raw service client that communicates directly with NINLCS service using only JSON and named pipes.
    /// No project references needed - completely self-contained.
    /// </summary>
    public class RawNinlcsClient : IDisposable
    {
        private NamedPipeClientStream? _pipeClient;
        private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
        private bool _disposed;

        /// <summary>
        /// Connect to the NINLCS service
        /// </summary>
        public async Task<ServiceResponse> ConnectAsync()
        {
            await _connectionSemaphore.WaitAsync();
            try
            {
                WriteDebugToFile("🔌 RawNinlcsClient.ConnectAsync() called");
                
                if (_pipeClient?.IsConnected == true)
                {
                    WriteDebugToFile("✅ Already connected to NINLCS service");
                    return new ServiceResponse { Success = true, Message = "Already connected" };
                }

                WriteDebugToFile("🔄 Creating new NamedPipeClientStream...");
                _pipeClient?.Dispose();
                _pipeClient = new NamedPipeClientStream(".", "NINLCS", PipeDirection.InOut);
                
                WriteDebugToFile("🔗 Attempting to connect to NINLCS named pipe...");
                await _pipeClient.ConnectAsync(5000);
                WriteDebugToFile("✅ Successfully connected to NINLCS service");
                
                return new ServiceResponse { Success = true, Message = "Connected to NINLCS service" };
            }
            catch (TimeoutException ex)
            {
                var errorMsg = $"Connection timeout: {ex.Message}";
                WriteDebugToFile($"⏰ {errorMsg}");
                return new ServiceResponse { Success = false, Message = errorMsg };
            }
            catch (System.IO.IOException ex)
            {
                var errorMsg = $"IO error connecting to NINLCS pipe: {ex.Message}";
                WriteDebugToFile($"💾 {errorMsg}");
                WriteDebugToFile($"💾 IO Exception HResult: 0x{ex.HResult:X8}");
                return new ServiceResponse { Success = false, Message = errorMsg };
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                var errorMsg = $"Win32 error: {ex.Message} (Error Code: {ex.ErrorCode}, Native: {ex.NativeErrorCode})";
                WriteDebugToFile($"🪟 {errorMsg}");
                return new ServiceResponse { Success = false, Message = errorMsg };
            }
            catch (UnauthorizedAccessException ex)
            {
                var errorMsg = $"Access denied to NINLCS pipe: {ex.Message}";
                WriteDebugToFile($"🔒 {errorMsg}");
                return new ServiceResponse { Success = false, Message = errorMsg };
            }
            catch (Exception ex)
            {
                var errorMsg = $"Connection failed: {ex.GetType().Name}: {ex.Message}";
                WriteDebugToFile($"❌ {errorMsg}");
                WriteDebugToFile($"❌ Stack trace: {ex.StackTrace}");
                return new ServiceResponse { Success = false, Message = errorMsg };
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// Debug logging helper
        /// </summary>
        private static void WriteDebugToFile(string message)
        {
            try
            {
                var debugFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "logs", "NucLedDebug.txt");
                var logsDirectory = Path.GetDirectoryName(debugFile);
                if (logsDirectory != null && !Directory.Exists(logsDirectory))
                {
                    Directory.CreateDirectory(logsDirectory);
                }
                File.AppendAllText(debugFile, $"{DateTime.Now:HH:mm:ss.fff} - {message}\n");
            }
            catch { /* ignore file errors */ }
        }

        /// <summary>
        /// Send a command to the service
        /// </summary>
        private async Task<ServiceResponse> SendCommandAsync(object command)
        {
            await _connectionSemaphore.WaitAsync();
            try
            {
                WriteDebugToFile($"📤 SendCommandAsync called with command type: {command.GetType().Name}");
                
                if (_pipeClient?.IsConnected != true)
                {
                    WriteDebugToFile("🔄 Not connected, attempting to connect...");
                    var connectResult = await ConnectAsync();
                    if (!connectResult.Success)
                    {
                        WriteDebugToFile($"❌ Connection failed: {connectResult.Message}");
                        return connectResult;
                    }
                }
                
                // Serialize command to JSON
                WriteDebugToFile("📝 Serializing command to JSON...");
                var commandJson = JsonSerializer.Serialize(command, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                WriteDebugToFile($"📝 Command JSON: {commandJson}");
                
                var commandBytes = System.Text.Encoding.UTF8.GetBytes(commandJson);
                WriteDebugToFile($"📏 Command size: {commandBytes.Length} bytes");
                
                // Write length prefix + command
                var lengthBytes = BitConverter.GetBytes(commandBytes.Length);
                await _pipeClient!.WriteAsync(lengthBytes, 0, lengthBytes.Length);
                await _pipeClient.WriteAsync(commandBytes, 0, commandBytes.Length);
                await _pipeClient.FlushAsync();
                WriteDebugToFile("✅ Command sent successfully");
                
                // Read response length
                WriteDebugToFile("📥 Reading response length...");
                var responseLengthBytes = new byte[sizeof(int)];
                await _pipeClient.ReadExactlyAsync(responseLengthBytes, 0, responseLengthBytes.Length);
                var responseLength = BitConverter.ToInt32(responseLengthBytes, 0);
                WriteDebugToFile($"📏 Response length: {responseLength} bytes");
                
                // Read response
                WriteDebugToFile("📥 Reading response data...");
                var responseBytes = new byte[responseLength];
                await _pipeClient.ReadExactlyAsync(responseBytes, 0, responseLength);
                var responseJson = System.Text.Encoding.UTF8.GetString(responseBytes);
                WriteDebugToFile($"📥 Response JSON: {responseJson}");
                
                // Deserialize response
                var response = JsonSerializer.Deserialize<ServiceResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                var result = response ?? new ServiceResponse { Success = false, Message = "Failed to parse response" };
                WriteDebugToFile($"✅ Command completed - Success: {result.Success}, Message: {result.Message}");
                return result;
            }
            catch (System.IO.IOException ex)
            {
                var errorMsg = $"IO error during command: {ex.Message}";
                WriteDebugToFile($"💾 {errorMsg}");
                WriteDebugToFile($"💾 IO Exception HResult: 0x{ex.HResult:X8}");
                return new ServiceResponse { Success = false, Message = errorMsg };
            }
            catch (JsonException ex)
            {
                var errorMsg = $"JSON error: {ex.Message}";
                WriteDebugToFile($"📝 {errorMsg}");
                return new ServiceResponse { Success = false, Message = errorMsg };
            }
            catch (Exception ex)
            {
                var errorMsg = $"Communication error: {ex.GetType().Name}: {ex.Message}";
                WriteDebugToFile($"❌ {errorMsg}");
                WriteDebugToFile($"❌ Stack trace: {ex.StackTrace}");
                return new ServiceResponse { Success = false, Message = errorMsg };
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// Set LED zone to solid color
        /// </summary>
        public async Task<ServiceResponse> SetZoneSolidAsync(string zone, string color, int brightness = 100)
        {
            var command = new
            {
                commandType = "SetZone",
                commandId = Guid.NewGuid(),
                configuration = new
                {
                    zone = GetZoneValue(zone),
                    pattern = GetPatternValue("Solid"),
                    primaryColor = new { name = color, r = GetColorR(color), g = GetColorG(color), b = GetColorB(color) },
                    brightness = new { percentage = brightness },
                    rainbowModeEnabled = false
                }
            };

            return await SendCommandAsync(command);
        }

        /// <summary>
        /// Set all zones to same color/pattern
        /// </summary>
        public async Task<ServiceResponse> SetAllZonesAsync(string pattern, string color, int brightness = 100, bool rainbowMode = false)
        {
            var zones = new[] { "Skull", "BottomLeft", "BottomRight", "BottomFront" };
            var results = new List<ServiceResponse>();

            foreach (var zone in zones)
            {
                var command = new
                {
                    commandType = "SetZone",
                    commandId = Guid.NewGuid(),
                    configuration = new
                    {
                        zone = GetZoneValue(zone),
                        pattern = GetPatternValue(pattern),
                        primaryColor = new { name = color, r = GetColorR(color), g = GetColorG(color), b = GetColorB(color) },
                        brightness = new { percentage = brightness },
                        rainbowModeEnabled = rainbowMode
                    }
                };

                var result = await SendCommandAsync(command);
                results.Add(result);
            }

            var failedCount = results.Count(r => !r.Success);
            if (failedCount == 0)
            {
                return new ServiceResponse { Success = true, Message = "All zones set successfully" };
            }
            else
            {
                return new ServiceResponse { Success = false, Message = $"{failedCount} zones failed" };
            }
        }

        /// <summary>
        /// Turn off all LEDs
        /// </summary>
        public async Task<ServiceResponse> TurnOffAllAsync()
        {
            var command = new
            {
                commandType = "TurnOffAll",
                commandId = Guid.NewGuid()
            };

            return await SendCommandAsync(command);
        }

        /// <summary>
        /// Enable rainbow mode on all zones
        /// </summary>
        public async Task<ServiceResponse> SetRainbowModeAsync(int brightness = 100)
        {
            return await SetAllZonesAsync("Rainbow", "Red", brightness, true);
        }

        /// <summary>
        /// Set a specific zone to a specific color
        /// </summary>
        public async Task<ServiceResponse> SetZoneColorAsync(string zone, string color, int brightness = 100)
        {
            return await SetZoneSolidAsync(zone, color, brightness);
        }

        /// <summary>
        /// Connect to hardware (if service isn't already connected)
        /// </summary>
        public async Task<ServiceResponse> ConnectToHardwareAsync(string comPort = "COM3")
        {
            var command = new
            {
                commandType = "Connect",
                commandId = Guid.NewGuid(),
                comPort = comPort
            };

            return await SendCommandAsync(command);
        }

        /// <summary>
        /// Get current LED status
        /// </summary>
        public async Task<ServiceResponse> GetStatusAsync()
        {
            var command = new
            {
                commandType = "GetStatus",
                commandId = Guid.NewGuid()
            };

            return await SendCommandAsync(command);
        }

        // Helper methods for color mapping
        private static int GetColorR(string color) => color.ToLower() switch
        {
            "red" => 255, "green" => 0, "blue" => 0, "yellow" => 255,
            "purple" => 255, "cyan" => 0, "white" => 255, "black" => 0,
            _ => 255
        };

        private static int GetColorG(string color) => color.ToLower() switch
        {
            "red" => 0, "green" => 255, "blue" => 0, "yellow" => 255,
            "purple" => 0, "cyan" => 255, "white" => 255, "black" => 0,
            _ => 0
        };

        private static int GetColorB(string color) => color.ToLower() switch
        {
            "red" => 0, "green" => 0, "blue" => 255, "yellow" => 0,
            "purple" => 255, "cyan" => 255, "white" => 255, "black" => 0,
            _ => 0
        };

        // Helper methods for enum mapping
        private static int GetZoneValue(string zone) => zone.ToLower() switch
        {
            "skull" => 0,           // LedZone.Skull
            "bottomleft" => 1,      // LedZone.BottomLeft
            "bottomright" => 2,     // LedZone.BottomRight
            "bottomfront" => 3,     // LedZone.BottomFront
            _ => 0                  // Default to Skull
        };

        private static int GetPatternValue(string pattern) => pattern.ToLower() switch
        {
            "off" => 0,             // LedPattern.Off
            "solid" => 1,           // LedPattern.Solid
            "pulse" => 2,           // LedPattern.Pulse
            "breathing" => 3,       // LedPattern.Breathing
            "strobing" => 4,        // LedPattern.Strobing
            "rainbow" => 5,         // LedPattern.Rainbow
            "rainbow2" => 6,        // LedPattern.Rainbow2
            "pulsetrain1" => 7,     // LedPattern.PulseTrain1
            "pulsetrain2" => 8,     // LedPattern.PulseTrain2
            "pulsetrain3" => 9,     // LedPattern.PulseTrain3
            _ => 1                  // Default to Solid
        };

        public void Dispose()
        {
            if (!_disposed)
            {
                _pipeClient?.Dispose();
                _connectionSemaphore.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Simple response class for service communication
    /// </summary>
    public class ServiceResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string? ErrorDetails { get; set; }
    }
}
