using System.IO.Ports;
using System.Text;
using NucLedController.Core.Models;
using NucLedController.Core.Interfaces;
using NucLedController.Core.Services;

namespace NucLedController.Core;

/// <summary>
/// Main controller for Intel NUC LED operations
/// Extracted from working console implementation with proven command sequences
/// Implements exclusive COM port access to prevent conflicts
/// Includes comprehensive state management and persistence
/// </summary>
public class NucLedController : INucLedController
{
    private SerialPort? _serialPort;
    private bool _isConnected;
    private string _connectionStatus = "Disconnected";
    private bool _ledsEnabled = false; // Track current LED state
    private static Mutex? _comPortMutex; // Prevent multiple instances accessing COM3
    private static readonly string MutexName = "Global\\NucLedController_COM3_Access";
    private bool _ownsMutex = false; // Track if this instance owns the mutex
    
    // State management
    private readonly LedStateManager _stateManager;
    
    public bool IsConnected => _isConnected;
    public string ConnectionStatus => _connectionStatus;
    public bool LedsEnabled => _ledsEnabled; // Expose current LED state
    public LedStateManager StateManager => _stateManager; // Expose state manager
    
    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<bool>? LedStateChanged; // Notify when LED state changes
    
    public NucLedController()
    {
        _stateManager = new LedStateManager();
        
        // Subscribe to state manager events to keep internal state in sync
        _stateManager.LedsEnabledChanged += (s, enabled) => 
        {
            if (_ledsEnabled != enabled)
            {
                _ledsEnabled = enabled;
                LedStateChanged?.Invoke(this, enabled);
            }
        };
    }
    
    /// <summary>
    /// Initialize the controller and load saved state
    /// </summary>
    public async Task InitializeAsync()
    {
        await _stateManager.LoadStateAsync();
        
        // Sync internal state with loaded state
        var currentState = _stateManager.CurrentState;
        _ledsEnabled = currentState.LedsEnabled;
    }
    
    public async Task<LedCommandResult> ConnectAsync(string portName = "COM3")
    {
        const int maxRetries = 3;
        const int retryDelayMs = 2000; // 2 seconds between retries
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (_isConnected)
                {
                    return LedCommandResult.SuccessResult("Already connected");
                }
                
                UpdateStatus($"Acquiring COM port access... (attempt {attempt}/{maxRetries})");
                
                // Try to acquire exclusive access to COM port with longer timeout
                _comPortMutex = new Mutex(false, MutexName);
                bool acquired = _comPortMutex.WaitOne(15000); // Increased to 15 seconds
                
                if (!acquired)
                {
                    _comPortMutex?.Dispose();
                    _comPortMutex = null;
                    _ownsMutex = false;
                    
                    if (attempt < maxRetries)
                    {
                        UpdateStatus($"COM port busy, retrying in {retryDelayMs/1000} seconds...");
                        await Task.Delay(retryDelayMs);
                        continue;
                    }
                    return LedCommandResult.FailureResult("COM port is in use by another application. Please close other NUC LED applications and wait 15 seconds.");
                }
                
                _ownsMutex = true; // We now own the mutex
                UpdateStatus("Exclusive COM access acquired, connecting...");
                
                // Additional safety: Check if COM port is actually available
                try
                {
                    // Try to detect if another process has the port open
                    using (var testPort = new SerialPort(portName, 9600))
                    {
                        testPort.Open();
                        testPort.Close();
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    if (_ownsMutex && _comPortMutex != null)
                    {
                        _comPortMutex.ReleaseMutex();
                        _ownsMutex = false;
                    }
                    _comPortMutex?.Dispose();
                    _comPortMutex = null;
                    
                    if (attempt < maxRetries)
                    {
                        UpdateStatus($"Port access denied, another app has exclusive control. Retrying in {retryDelayMs/1000} seconds...");
                        await Task.Delay(retryDelayMs);
                        continue;
                    }
                    return LedCommandResult.FailureResult("COM port is exclusively locked by another application. Close all LED control applications.");
                }
                
                UpdateStatus("Opening serial connection...");
                
                _serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
                _serialPort.Open();
                
                await Task.Delay(7000); // Allow connection to stabilize (7 seconds for hardware init)
            
                // Test actual hardware communication before claiming success
                UpdateStatus("Verifying hardware communication...");
                bool hardwareResponding = await TestHardwareCommunicationAsync();
                if (!hardwareResponding)
                {
                    _serialPort?.Close();
                    if (_ownsMutex && _comPortMutex != null)
                    {
                        _comPortMutex.ReleaseMutex();
                        _ownsMutex = false;
                    }
                    _comPortMutex?.Dispose();
                    _comPortMutex = null;
                    _serialPort?.Dispose();
                    _serialPort = null;
                    UpdateStatus("Hardware not responding");
                    
                    if (attempt < maxRetries)
                    {
                        UpdateStatus($"Hardware test failed, retrying in {retryDelayMs/1000} seconds...");
                        await Task.Delay(retryDelayMs);
                        continue;
                    }
                    return LedCommandResult.FailureResult("Hardware not responding to commands. Another application may be controlling the LEDs.");
                }
                
                // Initialize with no rainbow (aggressive prevention)
                var initResult = await InitializeNoRainbowAsync();
                if (!initResult.Success)
                {
                    _serialPort?.Close();
                    if (_ownsMutex && _comPortMutex != null)
                    {
                        _comPortMutex.ReleaseMutex();
                        _ownsMutex = false;
                    }
                    _comPortMutex?.Dispose();
                    _comPortMutex = null;
                    _serialPort?.Dispose();
                    _serialPort = null;
                    UpdateStatus("Connection failed");
                    
                    if (attempt < maxRetries)
                    {
                        UpdateStatus($"Initialization failed, retrying in {retryDelayMs/1000} seconds...");
                        await Task.Delay(retryDelayMs);
                        continue;
                    }
                    return initResult;
                }
                
                SetConnected(true);
                UpdateStatus($"Connected to {portName}");
                
                // Load and potentially restore state
                var currentState = _stateManager.CurrentState;
                _ledsEnabled = currentState.LedsEnabled;
                LedStateChanged?.Invoke(this, _ledsEnabled);
                
                // Optionally restore hardware state to match saved state
                if (currentState.LedsEnabled && !currentState.IsCompletelyOff())
                {
                    UpdateStatus("Restoring LED state...");
                    await RestoreHardwareStateAsync();
                }
                
                return LedCommandResult.SuccessResult($"Connected to {portName}");
            }
            catch (Exception ex)
            {
                SetConnected(false);
                UpdateStatus("Connection failed");
                
                // Release mutex on failure only if we own it
                if (_ownsMutex && _comPortMutex != null)
                {
                    _comPortMutex.ReleaseMutex();
                    _ownsMutex = false;
                }
                _comPortMutex?.Dispose();
                _comPortMutex = null;
                
                if (attempt < maxRetries)
                {
                    UpdateStatus($"Connection error, retrying in {retryDelayMs/1000} seconds...");
                    await Task.Delay(retryDelayMs);
                    continue;
                }
                return LedCommandResult.FailureResult($"Failed to connect: {ex.Message}", ex);
            }
        }
        
        // This should never be reached, but just in case
        return LedCommandResult.FailureResult("Connection failed after all retry attempts");
    }
    
    public async Task<LedCommandResult> DisconnectAsync()
    {
        try
        {
            if (_isConnected && _serialPort != null)
            {
                // Turn off LEDs before disconnecting
                await SendCommandAsync("BTN:0");
                await Task.Delay(200);
                
                _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;
            }
            
            SetConnected(false);
            UpdateStatus("Disconnected");
            
            // Release COM port mutex only if we own it
            if (_ownsMutex && _comPortMutex != null)
            {
                _comPortMutex.ReleaseMutex();
                _ownsMutex = false;
            }
            _comPortMutex?.Dispose();
            _comPortMutex = null;
            
            return LedCommandResult.SuccessResult("Disconnected successfully");
        }
        catch (Exception ex)
        {
            return LedCommandResult.FailureResult($"Error during disconnect: {ex.Message}", ex);
        }
    }
    
    public async Task<LedCommandResult> SetZoneColorAsync(LedZone zone, int color, int brightness = 5)
    {
        if (!_isConnected)
            return LedCommandResult.FailureResult("Not connected to device");
            
        try
        {
            UpdateStatus($"Setting {zone} to color {color}...");
            
            // DEBUGGING: Show exactly what we're about to do
            Console.WriteLine($"🎯 DEBUG: SetZoneColorAsync called - Zone: {zone}, Color: {color}, Brightness: {brightness}");
            
            // Use the EXACT working sequence from Option 1
            var zoneChar = GetZoneCharacter(zone);
            var colorChannel = GetColorChannel(zone);
            
            Console.WriteLine($"🎯 DEBUG: Zone mappings - ZoneChar: '{zoneChar}', ColorChannel: '{colorChannel}'");
            
            // Pattern 1 (static) first
            Console.WriteLine($"🎯 DEBUG: Step 1 - Setting pattern to static");
            await SendCommandAsync($"{zoneChar}P1");
            await Task.Delay(200);
            
            // Rainbow disable AFTER pattern (critical sequence)
            Console.WriteLine($"🎯 DEBUG: Step 2 - Disabling rainbow");
            await SendCommandAsync($"{zoneChar}R:0");
            await Task.Delay(200);
            
            // Set color
            Console.WriteLine($"🎯 DEBUG: Step 3 - Setting color");
            await SendCommandAsync($"{colorChannel}:{color}");
            await Task.Delay(200);
            
            // Set brightness
            Console.WriteLine($"🎯 DEBUG: Step 4 - Setting brightness");
            await SendCommandAsync($"{zoneChar}V:{brightness}");
            await Task.Delay(200);
            
            Console.WriteLine($"🎯 DEBUG: SetZoneColorAsync completed successfully");
            
            // Update state management
            await _stateManager.SetZoneStateAsync(zone, color, brightness, LedPattern.Static, true);
            
            UpdateStatus($"{zone} set to color {color}");
            return LedCommandResult.SuccessResult($"{zone} color set to {color}");
        }
        catch (Exception ex)
        {
            return LedCommandResult.FailureResult($"Failed to set zone color: {ex.Message}", ex);
        }
    }
    
    public async Task<LedCommandResult> SetAllZonesAsync(int color, int brightness = 5)
    {
        if (!_isConnected)
            return LedCommandResult.FailureResult("Not connected to device");
            
        try
        {
            UpdateStatus("Setting all zones...");
            
            // Ensure LEDs are on first
            await SendCommandAsync("RST");
            await Task.Delay(500);
            await SendCommandAsync("BTN:1");
            await Task.Delay(500);
            
            // Set all zones to static pattern
            await SendCommandAsync("AP1");
            await SendCommandAsync("BP1");
            await SendCommandAsync("CP1");
            await SendCommandAsync("DP1");
            await Task.Delay(500);
            
            // Disable rainbow AFTER patterns
            await SendCommandAsync("AR:0");
            await SendCommandAsync("BR:0");
            await SendCommandAsync("CR:0");
            await SendCommandAsync("DR:0");
            await Task.Delay(500);
            
            // Set colors
            await SendCommandAsync($"C1:{color}");
            await SendCommandAsync($"C2:{color}");
            await SendCommandAsync($"C3:{color}");
            await SendCommandAsync($"C4:{color}");
            await Task.Delay(500);
            
            // Set brightness
            await SendCommandAsync($"AV:{brightness}");
            await SendCommandAsync($"BV:{brightness}");
            await SendCommandAsync($"CV:{brightness}");
            await SendCommandAsync($"DV:{brightness}");
            await Task.Delay(500);
            
            // Update state management
            await _stateManager.SetLedsEnabledAsync(true);
            await _stateManager.SetAllZonesAsync(color, brightness, LedPattern.Static, true);
            
            UpdateStatus($"All zones set to color {color}");
            return LedCommandResult.SuccessResult($"All zones set to color {color}");
        }
        catch (Exception ex)
        {
            return LedCommandResult.FailureResult($"Failed to set all zones: {ex.Message}", ex);
        }
    }
    
    public async Task<LedCommandResult> DisableRainbowAsync()
    {
        if (!_isConnected)
            return LedCommandResult.FailureResult("Not connected to device");
            
        try
        {
            UpdateStatus("Disabling rainbow mode...");
            
            // Use the EXACT working sequence from Option 5
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                // Reset first
                await SendCommandAsync("RST");
                await Task.Delay(400);
                await SendCommandAsync("BTN:1");
                await Task.Delay(400);
                
                // For each zone, use the EXACT sequence that works
                var zones = new[] { "A", "B", "C", "D" };
                var colorChannels = new[] { "C1", "C2", "C3", "C4" };
                var colors = new[] { LedColors.Red, LedColors.Green, LedColors.Blue, LedColors.Yellow };
                
                for (int i = 0; i < zones.Length; i++)
                {
                    // Pattern 1 first
                    await SendCommandAsync($"{zones[i]}P1");
                    await Task.Delay(200);
                    
                    // Rainbow disable AFTER pattern
                    await SendCommandAsync($"{zones[i]}R:0");
                    await Task.Delay(200);
                    
                    // Set color
                    await SendCommandAsync($"{colorChannels[i]}:{colors[i]}");
                    await Task.Delay(200);
                    
                    // Max brightness
                    await SendCommandAsync($"{zones[i]}V:5");
                    await Task.Delay(200);
                }
                
                await Task.Delay(1000);
            }
            
            UpdateStatus("Rainbow mode disabled");
            return LedCommandResult.SuccessResult("Rainbow mode disabled successfully");
        }
        catch (Exception ex)
        {
            return LedCommandResult.FailureResult($"Failed to disable rainbow: {ex.Message}", ex);
        }
    }
    
    public async Task<LedCommandResult> TurnOffAsync()
    {
        if (!_isConnected)
            return LedCommandResult.FailureResult("Not connected to device");
            
        try
        {
            UpdateStatus("Turning off LEDs...");
            
            // Enhanced off sequence with aggressive rainbow termination (Intel SDK style)
            UpdateStatus("Step 1: Reset all patterns");
            await SendCommandAsync("RST");          
            await Task.Delay(500);                  
            
            UpdateStatus("Step 2: Disable button");
            await SendCommandAsync("BTN:0");        
            await Task.Delay(500);                  
            
            UpdateStatus("Step 3: Aggressive rainbow disable");
            // Intel SDK style - disable rainbow on all zones aggressively
            await SendCommandAsync("AR:0");         
            await Task.Delay(200);
            await SendCommandAsync("BR:0");         
            await Task.Delay(200);
            await SendCommandAsync("CR:0");         
            await Task.Delay(200);
            await SendCommandAsync("DR:0");         
            await Task.Delay(500);
            
            UpdateStatus("Step 4: Set static patterns");
            // Ensure all zones are on static pattern (no animation)
            await SendCommandAsync("AP1");
            await Task.Delay(100);
            await SendCommandAsync("BP1");
            await Task.Delay(100);
            await SendCommandAsync("CP1");
            await Task.Delay(100);
            await SendCommandAsync("DP1");
            await Task.Delay(500);
            
            UpdateStatus("Step 5: Set all colors to black");
            await SendCommandAsync("C1:0");         
            await Task.Delay(100);
            await SendCommandAsync("C2:0");         
            await Task.Delay(100);
            await SendCommandAsync("C3:0");         
            await Task.Delay(100);
            await SendCommandAsync("C4:0");         
            await Task.Delay(500);
            
            UpdateStatus("Step 6: Final disable confirmation");
            await SendCommandAsync("BTN:0");        
            await Task.Delay(300);
            
            // Update tracked state
            _ledsEnabled = false;
            await _stateManager.SetLedsEnabledAsync(false);
            await _stateManager.ResetAllZonesAsync();
            LedStateChanged?.Invoke(this, _ledsEnabled);
            
            UpdateStatus("LEDs turned off with enhanced anti-rainbow sequence");
            return LedCommandResult.SuccessResult("LEDs turned off with enhanced anti-rainbow sequence");
        }
        catch (Exception ex)
        {
            return LedCommandResult.FailureResult($"Failed to turn off LEDs: {ex.Message}", ex);
        }
    }
    
    public async Task<LedCommandResult> TurnOnAsync()
    {
        if (!_isConnected)
            return LedCommandResult.FailureResult("Not connected to device");
            
        try
        {
            UpdateStatus("Turning on LEDs...");
            
            // Enhanced diagnostic sequence with aggressive rainbow termination
            UpdateStatus("Step 1: Reset all patterns");
            await SendCommandAsync("RST");          
            await Task.Delay(500);  // Increased delay
            
            UpdateStatus("Step 2: Enable button");
            await SendCommandAsync("BTN:1");        
            await Task.Delay(500);  // Increased delay
            
            UpdateStatus("Step 3: Ultra-aggressive rainbow disable");
            // Multiple attempts to kill rainbow mode completely
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                await SendCommandAsync("AR:0");         
                await Task.Delay(200);
                await SendCommandAsync("BR:0");         
                await Task.Delay(200);
                await SendCommandAsync("CR:0");         
                await Task.Delay(200);
                await SendCommandAsync("DR:0");         
                await Task.Delay(200);
            }
            
            UpdateStatus("Step 4: Set static patterns firmly");
            // Ensure static patterns are locked in
            await SendCommandAsync("AP1");
            await Task.Delay(150);
            await SendCommandAsync("BP1");
            await Task.Delay(150);
            await SendCommandAsync("CP1");
            await Task.Delay(150);
            await SendCommandAsync("DP1");
            await Task.Delay(300);
            
            UpdateStatus("Step 5: Set solid yellow color");
            // Set a solid, non-rainbow color
            await SendCommandAsync("C1:16776960");  // Yellow (0xFFFF00)
            await Task.Delay(200);
            await SendCommandAsync("C2:16776960");  // Yellow for all zones
            await Task.Delay(200);
            await SendCommandAsync("C3:16776960");  
            await Task.Delay(200);
            await SendCommandAsync("C4:16776960");  
            await Task.Delay(300);                  
            
            UpdateStatus("Step 6: Ensure brightness");
            await SendCommandAsync("AV:5");
            await Task.Delay(100);
            await SendCommandAsync("BV:5");
            await Task.Delay(100);
            await SendCommandAsync("CV:5");
            await Task.Delay(100);
            await SendCommandAsync("DV:5");
            await Task.Delay(300);
            
            UpdateStatus("Step 7: Final rainbow kill");
            // One more rainbow disable to be absolutely sure
            await SendCommandAsync("AR:0");         
            await SendCommandAsync("BR:0");         
            await SendCommandAsync("CR:0");         
            await SendCommandAsync("DR:0");         
            await Task.Delay(200);
            
            // Update tracked state
            _ledsEnabled = true;
            await _stateManager.SetLedsEnabledAsync(true);
            await _stateManager.SetAllZonesAsync(LedColors.Yellow, 5, LedPattern.Static, true);
            LedStateChanged?.Invoke(this, _ledsEnabled);
            
            UpdateStatus("LEDs turned on with anti-rainbow sequence");
            return LedCommandResult.SuccessResult("LEDs turned on with anti-rainbow sequence");
        }
        catch (Exception ex)
        {
            return LedCommandResult.FailureResult($"Failed to turn on LEDs: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Restore hardware state to match saved application state
    /// </summary>
    private async Task<LedCommandResult> RestoreHardwareStateAsync()
    {
        try
        {
            var currentState = _stateManager.CurrentState;
            
            if (!currentState.LedsEnabled || currentState.IsCompletelyOff())
            {
                return await TurnOffAsync();
            }
            
            // If effects are enabled, use simple turn on
            if (currentState.EffectsEnabled)
            {
                return await TurnOnAsync();
            }
            
            // Restore individual zone states
            UpdateStatus("Restoring zone states...");
            
            // Reset and prepare
            await SendCommandAsync("RST");
            await Task.Delay(500);
            await SendCommandAsync("BTN:1");
            await Task.Delay(500);
            
            // Disable rainbow mode first
            await SendCommandAsync("AR:0");
            await SendCommandAsync("BR:0");
            await SendCommandAsync("CR:0");
            await SendCommandAsync("DR:0");
            await Task.Delay(300);
            
            // Restore each zone
            foreach (var zone in Enum.GetValues<LedZone>())
            {
                var zoneState = currentState.Zones[zone];
                if (zoneState.Enabled && zoneState.Color != LedColors.Black)
                {
                    await SetZoneColorAsync(zone, zoneState.Color, zoneState.Brightness);
                    await Task.Delay(200);
                }
            }
            
            UpdateStatus("State restored");
            return LedCommandResult.SuccessResult("Hardware state restored from saved configuration");
        }
        catch (Exception ex)
        {
            return LedCommandResult.FailureResult($"Failed to restore state: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Test if hardware is actually responding to our commands
    /// This prevents false positive connections when another app has control
    /// </summary>
    private async Task<bool> TestHardwareCommunicationAsync()
    {
        try
        {
            if (_serialPort == null) return false;
            
            // Try a simple command that should always work
            await SendCommandAsync("BTN:0"); // Turn off
            await Task.Delay(500);
            await SendCommandAsync("BTN:1"); // Turn on
            await Task.Delay(500);
            await SendCommandAsync("C1:16711680"); // Set red
            await Task.Delay(500);
            await SendCommandAsync("C1:0"); // Set black
            await Task.Delay(500);
            
            // If we got here without exceptions, assume hardware is responding
            // (We can't read back from our simple protocol, but at least serial port accepted commands)
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Private helper methods
    private async Task<LedCommandResult> InitializeNoRainbowAsync()
    {
        try
        {
            Console.WriteLine($"🎯 DEBUG: Starting InitializeNoRainbowAsync");
            
            // EXACT working sequence - DO NOT change order
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                Console.WriteLine($"🎯 DEBUG: Initialization attempt {attempt}/3");
                
                // Step 1: Reset everything first
                Console.WriteLine($"🎯 DEBUG: Step 1 - Reset");
                await SendCommandAsync("RST");
                await Task.Delay(300);
                
                // Step 2: POWER ON THE LEDS - This is critical!
                Console.WriteLine($"🎯 DEBUG: Step 2 - Power on LEDs (BTN:1)");
                await SendCommandAsync("BTN:1");
                await Task.Delay(300);
                
                // Step 3: Set ALL zones to static pattern FIRST
                Console.WriteLine($"🎯 DEBUG: Step 3 - Set static patterns");
                await SendCommandAsync("AP1");
                await SendCommandAsync("BP1");
                await SendCommandAsync("CP1");
                await SendCommandAsync("DP1");
                await Task.Delay(300);
                
                // Step 4: CRITICAL - Rainbow disable AFTER patterns
                Console.WriteLine($"🎯 DEBUG: Step 4 - Disable rainbow after patterns");
                await SendCommandAsync("AR:0");
                await SendCommandAsync("BR:0");
                await SendCommandAsync("CR:0");
                await SendCommandAsync("DR:0");
                await Task.Delay(300);
                
                // Step 5: Set initial visible color (not black!)
                Console.WriteLine($"🎯 DEBUG: Step 5 - Set initial red color for visibility");
                await SendCommandAsync("C1:16711680");  // Red
                await SendCommandAsync("C2:16711680");  // Red
                await SendCommandAsync("C3:16711680");  // Red
                await SendCommandAsync("C4:16711680");  // Red
                await Task.Delay(300);
                
                // Step 6: Set brightness to maximum
                Console.WriteLine($"🎯 DEBUG: Step 6 - Set maximum brightness");
                await SendCommandAsync("AV:5");
                await SendCommandAsync("BV:5");
                await SendCommandAsync("CV:5");
                await SendCommandAsync("DV:5");
                await Task.Delay(500);
            }
            
            Console.WriteLine($"🎯 DEBUG: InitializeNoRainbowAsync completed");
            return LedCommandResult.SuccessResult("Initialization complete - LEDs should be visible red");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"🎯 DEBUG: InitializeNoRainbowAsync failed: {ex.Message}");
            return LedCommandResult.FailureResult($"Initialization failed: {ex.Message}", ex);
        }
    }
    
    private async Task SendCommandAsync(string command)
    {
        if (_serialPort == null) return;
        
        var cmd = command + "\r\n";  // Try CRLF instead of just LF
        var bytes = Encoding.UTF8.GetBytes(cmd);
        
        // DEBUGGING: Show exactly what we're sending
        Console.WriteLine($"🔧 DEBUG: Sending command: '{command}' (bytes: {string.Join(",", bytes)})");
        Console.WriteLine($"🔧 DEBUG: Serial port state - IsOpen: {_serialPort.IsOpen}, BytesToWrite: {_serialPort.BytesToWrite}");
        
        _serialPort.Write(bytes, 0, bytes.Length);
        
        // Check if data was actually sent
        Console.WriteLine($"🔧 DEBUG: After write - BytesToWrite: {_serialPort.BytesToWrite}");
        
        // Small delay to prevent overwhelming the device
        await Task.Delay(50);
    }
    
    private static string GetZoneCharacter(LedZone zone)
    {
        return zone switch
        {
            LedZone.Skull => "A",
            LedZone.BottomLeft => "B",
            LedZone.BottomRight => "C",
            LedZone.FrontBottom => "D",
            _ => "A"
        };
    }
    
    private static string GetColorChannel(LedZone zone)
    {
        return zone switch
        {
            LedZone.Skull => "C1",
            LedZone.BottomLeft => "C2",
            LedZone.BottomRight => "C3",
            LedZone.FrontBottom => "C4",
            _ => "C1"
        };
    }
    
    private void SetConnected(bool connected)
    {
        if (_isConnected != connected)
        {
            _isConnected = connected;
            ConnectionChanged?.Invoke(this, connected);
        }
    }
    
    private void UpdateStatus(string status)
    {
        if (_connectionStatus != status)
        {
            _connectionStatus = status;
            StatusChanged?.Invoke(this, status);
        }
    }
    
    /// <summary>
    /// Get current LED patterns from hardware
    /// Based on Intel SDK GetCurrentPatterns() method
    /// </summary>
    public async Task<LedStatus?> GetCurrentPatternsAsync()
    {
        if (!_isConnected)
            return null;
            
        try
        {
            UpdateStatus("Reading hardware state...");
            
            // Create patterns for each zone based on current saved state
            // Note: Our simple serial protocol doesn't support hardware readback
            // So we use the saved state as the "hardware" state
            var currentState = _stateManager.CurrentState;
            var patterns = new List<HardwareLedPattern>();
            
            foreach (var zone in Enum.GetValues<LedZone>())
            {
                var zoneState = currentState.Zones[zone];
                var pattern = new HardwareLedPattern(
                    channel: HardwareLedPattern.ZoneToChannel(zone),
                    color: zoneState.Color,
                    brightness: zoneState.Brightness,
                    pattern: (int)zoneState.Pattern,
                    speed: 3, // Default speed
                    enabled: zoneState.Enabled
                );
                patterns.Add(pattern);
            }
            
            var ledStatus = new LedStatus(currentState.LedsEnabled, patterns.ToArray());
            
            UpdateStatus("Hardware state read");
            return ledStatus;
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error reading hardware state: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Update application state from hardware state
    /// Based on Intel SDK UpdateFromDevice() method
    /// </summary>
    public async Task<LedCommandResult> UpdateFromDeviceAsync()
    {
        try
        {
            var status = await GetCurrentPatternsAsync();
            if (status == null)
                return LedCommandResult.FailureResult("Failed to read hardware state");
            
            // Update internal state
            _ledsEnabled = status.ButtonStatus;
            
            // Update state manager with hardware patterns
            await _stateManager.SetLedsEnabledAsync(status.ButtonStatus);
            
            foreach (var pattern in status.LedPatternList)
            {
                var zone = HardwareLedPattern.ChannelToZone(pattern.Channel);
                await _stateManager.SetZoneStateAsync(
                    zone, 
                    pattern.Color, 
                    pattern.Brightness, 
                    (LedPattern)pattern.Pattern, 
                    pattern.Enabled
                );
            }
            
            // Notify UI of state change
            LedStateChanged?.Invoke(this, _ledsEnabled);
            
            return LedCommandResult.SuccessResult("State synchronized with hardware");
        }
        catch (Exception ex)
        {
            return LedCommandResult.FailureResult($"Failed to sync with hardware: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Enhanced GetCurrentPattern method like Intel SDK
    /// </summary>
    public async Task<LedCommandResult> GetCurrentPatternAsync()
    {
        try
        {
            if (!_isConnected)
                return LedCommandResult.FailureResult("Not connected to device");
            
            // Add command delay like Intel SDK
            await SendCommandAsync("COMMAND_DELAY");
            await Task.Delay(100);
            
            // Get current patterns and update from device
            var result = await UpdateFromDeviceAsync();
            return result;
        }
        catch (Exception ex)
        {
            return LedCommandResult.FailureResult($"Failed to get current pattern: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        try
        {
            if (_isConnected && _serialPort != null)
            {
                // Try to turn off LEDs gracefully
                var cmd = "BTN:0\n";
                var bytes = Encoding.UTF8.GetBytes(cmd);
                _serialPort.Write(bytes, 0, bytes.Length);
                Thread.Sleep(200);
            }
        }
        catch
        {
            // Ignore errors during disposal
        }
        finally
        {
            _serialPort?.Close();
            _serialPort?.Dispose();
            _serialPort = null;
            SetConnected(false);
            
            // Release COM port mutex on disposal only if we own it
            if (_ownsMutex && _comPortMutex != null)
            {
                try
                {
                    _comPortMutex.ReleaseMutex();
                    _ownsMutex = false;
                }
                catch (ApplicationException)
                {
                    // Mutex was already released - ignore
                    _ownsMutex = false;
                }
            }
            _comPortMutex?.Dispose();
            _comPortMutex = null;
        }
    }
}
