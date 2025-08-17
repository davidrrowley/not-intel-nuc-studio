using System;
using System.IO;
using System.Threading.Tasks;

#nullable enable

namespace NotIntelNucStudio.WinUI3.Services
{
    /// <summary>
    /// Singleton service manager for LED control - provides a single shared connection to NINLCS service
    /// </summary>
    public class LedServiceManager
    {
        private static LedServiceManager? _instance;
        private static readonly object _lock = new object();
        private NinlcsClient? _serviceClient;
        private bool _isConnected = false;
        private bool _isConnecting = false;

        private LedServiceManager() { }

        public static LedServiceManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LedServiceManager();
                        }
                    }
                }
                return _instance;
            }
        }

        public bool IsConnected => _isConnected;

        public async Task<CommandResponse> EnsureConnectedAsync()
        {
            if (_isConnected && _serviceClient != null)
            {
                WriteDebugToFile("✅ Already connected to NINLCS service");
                return CommandResponse.SuccessResult(Guid.NewGuid(), "Already connected");
            }

            if (_isConnecting)
            {
                WriteDebugToFile("⏳ Connection already in progress, waiting...");
                // Wait a bit for the connection to complete
                for (int i = 0; i < 50; i++) // 5 second timeout
                {
                    if (_isConnected) break;
                    await Task.Delay(100);
                }
                return _isConnected 
                    ? CommandResponse.SuccessResult(Guid.NewGuid(), "Connected")
                    : CommandResponse.FailureResult(Guid.NewGuid(), "Connection timeout");
            }

            _isConnecting = true;
            try
            {
                WriteDebugToFile("🔌 Creating new NINLCS service connection...");
                _serviceClient?.Dispose();
                _serviceClient = new NinlcsClient();

                var connectResult = await _serviceClient.ConnectToServiceAsync();
                WriteDebugToFile($"🔌 Connection result: Success={connectResult.Success}, Message={connectResult.Message}");

                if (connectResult.Success)
                {
                    // Try to ensure hardware connection
                    WriteDebugToFile("🔌 Attempting to connect service to hardware...");
                    var hardwareResult = await _serviceClient.ConnectToHardwareAsync("COM3");
                    WriteDebugToFile($"🔌 Hardware connection result: Success={hardwareResult.Success}, Message={hardwareResult.Message}");

                    _isConnected = true;
                    WriteDebugToFile("✅ NINLCS service connection established successfully");
                    return CommandResponse.SuccessResult(Guid.NewGuid(), "Connected successfully");
                }
                else
                {
                    _isConnected = false;
                    WriteDebugToFile($"❌ Failed to connect to NINLCS service: {connectResult.Message}");
                    return connectResult;
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                var errorMsg = $"Service connection error: {ex.Message}";
                WriteDebugToFile($"💥 {errorMsg}");
                return CommandResponse.FailureResult(Guid.NewGuid(), errorMsg);
            }
            finally
            {
                _isConnecting = false;
            }
        }

        public async Task<CommandResponse> SetZoneColorAsync(string zone, string color, int brightness = 100)
        {
            var ensureResult = await EnsureConnectedAsync();
            if (!ensureResult.Success)
                return ensureResult;

            if (_serviceClient == null)
                return CommandResponse.FailureResult(Guid.NewGuid(), "Service client not available");

            try
            {
                WriteDebugToFile($"🎨 Setting zone {zone} to {color} with brightness {brightness}%");
                
                // Convert string parameters to enums
                var ledZone = StringToLedZone(zone);
                var ledColor = StringToLedColor(color);
                var ledBrightness = new LedBrightness(brightness);
                
                // Create zone configuration using the working console app API
                var config = ZoneConfiguration.Solid(ledZone, ledColor, ledBrightness);
                
                var result = await _serviceClient.SetZoneAsync(config);
                WriteDebugToFile($"🎨 Zone set result: Success={result.Success}, Message={result.Message}");
                
                return result;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error setting zone color: {ex.Message}";
                WriteDebugToFile($"💥 {errorMsg}");
                return CommandResponse.FailureResult(Guid.NewGuid(), errorMsg);
            }
        }

        public async Task<CommandResponse> SetZonePatternAsync(string zone, string pattern, string color, int brightness = 100)
        {
            var ensureResult = await EnsureConnectedAsync();
            if (!ensureResult.Success)
                return ensureResult;

            if (_serviceClient == null)
                return CommandResponse.FailureResult(Guid.NewGuid(), "Service client not available");

            try
            {
                WriteDebugToFile($"🎨 Setting zone {zone} to pattern {pattern} with color {color} and brightness {brightness}%");
                
                // Convert string parameters to enums
                var ledZone = StringToLedZone(zone);
                var ledPattern = StringToLedPattern(pattern);
                var ledColor = StringToLedColor(color);
                var ledBrightness = new LedBrightness(brightness);
                
                // Create zone configuration
                var config = new ZoneConfiguration
                {
                    Zone = ledZone,
                    Pattern = ledPattern,
                    PrimaryColor = ledColor,
                    Brightness = ledBrightness,
                    RainbowModeEnabled = pattern == "Rainbow"
                };
                
                var result = await _serviceClient.SetZoneAsync(config);
                WriteDebugToFile($"🎨 Set zone pattern result: Success={result.Success}, Message={result.Message}");
                return result;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error setting zone pattern: {ex.Message}";
                WriteDebugToFile($"💥 {errorMsg}");
                return CommandResponse.FailureResult(Guid.NewGuid(), errorMsg);
            }
        }

        public async Task<CommandResponse> SetAllZonesAsync(string pattern, string color, int brightness = 100, bool rainbowMode = false)
        {
            var ensureResult = await EnsureConnectedAsync();
            if (!ensureResult.Success)
                return ensureResult;

            if (_serviceClient == null)
                return CommandResponse.FailureResult(Guid.NewGuid(), "Service client not available");

            try
            {
                WriteDebugToFile($"🌈 Setting all zones to {pattern} {color} with brightness {brightness}%, rainbow={rainbowMode}");
                
                // Convert string parameters to enums - EXACTLY like working console app
                var ledPattern = StringToLedPattern(pattern);
                var ledColor = StringToLedColor(color);
                var ledBrightness = new LedBrightness(brightness);
                
                var result = await _serviceClient.SetAllZonesAsync(ledPattern, ledColor, null, null, ledBrightness, rainbowMode);
                WriteDebugToFile($"🌈 All zones set result: Success={result.Success}, Message={result.Message}");
                
                return result;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error setting all zones: {ex.Message}";
                WriteDebugToFile($"💥 {errorMsg}");
                return CommandResponse.FailureResult(Guid.NewGuid(), errorMsg);
            }
        }

        public async Task<CommandResponse> TurnOffAllAsync()
        {
            var ensureResult = await EnsureConnectedAsync();
            if (!ensureResult.Success)
                return ensureResult;

            if (_serviceClient == null)
                return CommandResponse.FailureResult(Guid.NewGuid(), "Service client not available");

            try
            {
                WriteDebugToFile("🔴 Turning off all LEDs");
                var result = await _serviceClient.TurnOffAllAsync();
                WriteDebugToFile($"🔴 Turn off result: Success={result.Success}, Message={result.Message}");
                return result;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error turning off LEDs: {ex.Message}";
                WriteDebugToFile($"💥 {errorMsg}");
                return CommandResponse.FailureResult(Guid.NewGuid(), errorMsg);
            }
        }

        public async Task<StatusResponse> GetStatusAsync()
        {
            var ensureResult = await EnsureConnectedAsync();
            if (!ensureResult.Success)
            {
                return new StatusResponse 
                { 
                    CommandId = Guid.NewGuid(),
                    Success = false, 
                    Message = ensureResult.Message,
                    IsServiceConnected = false,
                    IsHardwareConnected = false
                };
            }

            if (_serviceClient == null)
            {
                return new StatusResponse 
                { 
                    CommandId = Guid.NewGuid(),
                    Success = false, 
                    Message = "Service client not available",
                    IsServiceConnected = false,
                    IsHardwareConnected = false
                };
            }

            try
            {
                var result = await _serviceClient.GetStatusAsync();
                WriteDebugToFile($"📊 Status result: Success={result.Success}, ServiceConnected={result.IsServiceConnected}, HardwareConnected={result.IsHardwareConnected}");
                return result;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error getting status: {ex.Message}";
                WriteDebugToFile($"💥 {errorMsg}");
                return new StatusResponse 
                { 
                    CommandId = Guid.NewGuid(),
                    Success = false, 
                    Message = errorMsg,
                    IsServiceConnected = false,
                    IsHardwareConnected = false
                };
            }
        }

        // Helper methods to convert strings to enums - matching console app API
        private static LedZone StringToLedZone(string zone) => zone.ToLower() switch
        {
            "skull" => LedZone.Skull,
            "bottomleft" => LedZone.BottomLeft,
            "bottomright" => LedZone.BottomRight,
            "bottomfront" => LedZone.BottomFront,
            _ => LedZone.Skull
        };

        private static LedPattern StringToLedPattern(string pattern) => pattern.ToLower() switch
        {
            "solid" => LedPattern.Solid,
            "pulse" => LedPattern.Pulse,
            "breathing" => LedPattern.Breathing,
            "strobing" => LedPattern.Strobing,
            "pulsetrain1" => LedPattern.PulseTrain1,
            "pulsetrain2" => LedPattern.PulseTrain2,
            "pulsetrain3" => LedPattern.PulseTrain3,
            "rainbow" => LedPattern.Rainbow,
            "rainbow2" => LedPattern.Rainbow2,
            "off" => LedPattern.Off,
            _ => LedPattern.Solid
        };

        private static LedColor StringToLedColor(string color) => color.ToLower() switch
        {
            "red" => StandardColors.Red,
            "green" => StandardColors.Green,
            "blue" => StandardColors.Blue,
            "yellow" => StandardColors.Yellow,
            "purple" => StandardColors.Purple,
            "cyan" => StandardColors.Cyan,
            "white" => StandardColors.White,
            "orange" => StandardColors.Orange,
            "pink" => StandardColors.Pink,
            "black" => StandardColors.Black,
            _ => StandardColors.Red
        };

        private static void WriteDebugToFile(string message)
        {
            try
            {
                var debugFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "logs", "NucLedDebug.txt");
                var logsDirectory = Path.GetDirectoryName(debugFile);
                if (logsDirectory != null && !Directory.Exists(logsDirectory))
                {
                    Directory.CreateDirectory(logsDirectory);
                }
                File.AppendAllText(debugFile, $"{DateTime.Now:HH:mm:ss.fff} - {message}\n");
            }
            catch { /* ignore file errors */ }
        }

        public void Dispose()
        {
            _serviceClient?.Dispose();
            _serviceClient = null;
            _isConnected = false;
        }
    }
}
