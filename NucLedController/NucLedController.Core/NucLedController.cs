using System.IO.Ports;
using System.Text;
using NucLedController.Core.Models;
using NucLedController.Core.Interfaces;

namespace NucLedController.Core;

/// <summary>
/// Main controller for Intel NUC LED operations
/// Extracted from working console implementation with proven command sequences
/// </summary>
public class NucLedController : INucLedController
{
    private SerialPort? _serialPort;
    private bool _isConnected;
    private string _connectionStatus = "Disconnected";
    
    public bool IsConnected => _isConnected;
    public string ConnectionStatus => _connectionStatus;
    
    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<string>? StatusChanged;
    
    public async Task<LedCommandResult> ConnectAsync(string portName = "COM3")
    {
        try
        {
            if (_isConnected)
            {
                return LedCommandResult.SuccessResult("Already connected");
            }
            
            UpdateStatus("Connecting...");
            
            _serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
            _serialPort.Open();
            
            await Task.Delay(500); // Allow connection to stabilize
            
            // Initialize with no rainbow (aggressive prevention)
            var initResult = await InitializeNoRainbowAsync();
            if (!initResult.Success)
            {
                _serialPort?.Close();
                _serialPort?.Dispose();
                _serialPort = null;
                UpdateStatus("Connection failed");
                return initResult;
            }
            
            SetConnected(true);
            UpdateStatus($"Connected to {portName}");
            
            return LedCommandResult.SuccessResult($"Connected to {portName}");
        }
        catch (Exception ex)
        {
            SetConnected(false);
            UpdateStatus("Connection failed");
            return LedCommandResult.FailureResult($"Failed to connect: {ex.Message}", ex);
        }
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
            
            // Use the EXACT working sequence from Option 1
            var zoneChar = GetZoneCharacter(zone);
            var colorChannel = GetColorChannel(zone);
            
            // Pattern 1 (static) first
            await SendCommandAsync($"{zoneChar}P1");
            await Task.Delay(200);
            
            // Rainbow disable AFTER pattern (critical sequence)
            await SendCommandAsync($"{zoneChar}R:0");
            await Task.Delay(200);
            
            // Set color
            await SendCommandAsync($"{colorChannel}:{color}");
            await Task.Delay(200);
            
            // Set brightness
            await SendCommandAsync($"{zoneChar}V:{brightness}");
            await Task.Delay(200);
            
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
            await SendCommandAsync("BTN:0");
            await Task.Delay(200);
            UpdateStatus("LEDs turned off");
            return LedCommandResult.SuccessResult("LEDs turned off");
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
            
            // Reset and enable
            await SendCommandAsync("RST");
            await Task.Delay(500);
            await SendCommandAsync("BTN:1");
            await Task.Delay(500);
            
            // Set all zones to static red with no rainbow
            await InitializeNoRainbowAsync();
            
            UpdateStatus("LEDs turned on");
            return LedCommandResult.SuccessResult("LEDs turned on with static red");
        }
        catch (Exception ex)
        {
            return LedCommandResult.FailureResult($"Failed to turn on LEDs: {ex.Message}", ex);
        }
    }
    
    // Private helper methods
    private async Task<LedCommandResult> InitializeNoRainbowAsync()
    {
        try
        {
            // Multiple attempts to kill rainbow on startup (from working initialization)
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                // Reset 
                await SendCommandAsync("RST");
                await Task.Delay(300);
                await SendCommandAsync("BTN:1");
                await Task.Delay(300);
                
                // Set ALL zones to static pattern
                await SendCommandAsync("AP1");
                await SendCommandAsync("BP1");
                await SendCommandAsync("CP1");
                await SendCommandAsync("DP1");
                await Task.Delay(300);
                
                // CRITICAL: Rainbow disable AFTER patterns
                await SendCommandAsync("AR:0");
                await SendCommandAsync("BR:0");
                await SendCommandAsync("CR:0");
                await SendCommandAsync("DR:0");
                await Task.Delay(300);
                
                // Set red color for visibility
                await SendCommandAsync("C1:0");
                await SendCommandAsync("C2:0");
                await SendCommandAsync("C3:0");
                await SendCommandAsync("C4:0");
                await Task.Delay(300);
                
                // Max brightness
                await SendCommandAsync("AV:5");
                await SendCommandAsync("BV:5");
                await SendCommandAsync("CV:5");
                await SendCommandAsync("DV:5");
                await Task.Delay(500);
            }
            
            return LedCommandResult.SuccessResult("Initialization complete");
        }
        catch (Exception ex)
        {
            return LedCommandResult.FailureResult($"Initialization failed: {ex.Message}", ex);
        }
    }
    
    private async Task SendCommandAsync(string command)
    {
        if (_serialPort == null) return;
        
        var cmd = command + "\n";
        var bytes = Encoding.UTF8.GetBytes(cmd);
        _serialPort.Write(bytes, 0, bytes.Length);
        
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
        }
    }
}
