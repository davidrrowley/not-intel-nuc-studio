using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

// ============================================================================
// NINLCS ALL-IN-ONE CLIENT
// Complete Intel NUC LED Service client - copy this entire file to your project
// ============================================================================

namespace NotIntelNucStudio.WinUI3.Services;

// ============================================================================
// ENUMS AND MODELS
// ============================================================================

/// <summary>
/// Represents the four LED zones available on Intel NUC devices
/// </summary>
public enum LedZone
{
    /// <summary>Zone A - Skull (logo header)</summary>
    Skull = 0,
    /// <summary>Zone B - Bottom Left</summary>
    BottomLeft = 1,
    /// <summary>Zone C - Bottom Right</summary>
    BottomRight = 2,
    /// <summary>Zone D - Front Bottom</summary>
    BottomFront = 3
}

/// <summary>
/// LED patterns available for Intel NUC LED zones
/// </summary>
public enum LedPattern
{
    /// <summary>LEDs are turned off</summary>
    Off = 0,
    /// <summary>Static solid color (P1)</summary>
    Solid = 1,
    /// <summary>Slow pulse effect (P2)</summary>
    Pulse = 2,
    /// <summary>Breathing effect (P3)</summary>
    Breathing = 3,
    /// <summary>Fast strobe effect (P4)</summary>
    Strobing = 4,
    /// <summary>3-color pulse train sequence (P5)</summary>
    PulseTrain1 = 5,
    /// <summary>3-color pulse train sequence (P6)</summary>
    PulseTrain2 = 6,
    /// <summary>3-color pulse train sequence (P7)</summary>
    PulseTrain3 = 7,
    /// <summary>Rainbow color cycling mode (R:1)</summary>
    Rainbow = 8,
    /// <summary>Alternative rainbow color cycling mode (R:2)</summary>
    Rainbow2 = 9
}

/// <summary>
/// Represents LED brightness as a percentage (0-100%)
/// </summary>
public record LedBrightness
{
    private readonly int _percentage;
    
    public LedBrightness(int percentage)
    {
        _percentage = Math.Clamp(percentage, 0, 100);
    }
    
    /// <summary>Brightness as percentage (0-100)</summary>
    public int Percentage => _percentage;
    
    /// <summary>Convert to Intel NUC brightness value (0-5)</summary>
    public int ToNucBrightnessValue()
    {
        return (int)Math.Round(_percentage / 20.0); // 0-100% -> 0-5
    }
    
    /// <summary>Create from NUC brightness value (0-5)</summary>
    public static LedBrightness FromNucBrightnessValue(int nucValue)
    {
        var percentage = Math.Clamp(nucValue, 0, 5) * 20;
        return new LedBrightness(percentage);
    }
    
    // Predefined brightness levels
    public static LedBrightness Off => new(0);
    public static LedBrightness Low => new(20);
    public static LedBrightness Medium => new(60);
    public static LedBrightness High => new(80);
    public static LedBrightness Maximum => new(100);
    
    public override string ToString() => $"{_percentage}%";
}

/// <summary>
/// Represents an LED color with RGB values and name
/// </summary>
public record LedColor(byte Red, byte Green, byte Blue, string Name)
{
    /// <summary>Convert RGB to Intel NUC color value (0-255 hue approximation)</summary>
    public int ToNucColorValue()
    {
        // Convert RGB to HSV and extract hue for NUC color mapping
        var max = Math.Max(Math.Max(Red, Green), Blue);
        var min = Math.Min(Math.Min(Red, Green), Blue);
        
        if (max == min) return 0; // Grayscale -> Red
        
        var delta = max - min;
        var hue = 0.0;
        
        if (max == Red)
            hue = ((Green - Blue) / (double)delta) % 6;
        else if (max == Green)
            hue = (Blue - Red) / (double)delta + 2;
        else
            hue = (Red - Green) / (double)delta + 4;
            
        hue *= 60;
        if (hue < 0) hue += 360;
        
        // Map 0-360 degrees to 0-255 NUC range
        return (int)(hue * 255 / 360);
    }
    
    public override string ToString() => $"{Name} (R:{Red}, G:{Green}, B:{Blue})";
}

/// <summary>
/// Standard LED colors for Intel NUC
/// </summary>
public static class StandardColors
{
    public static readonly LedColor Red = new(255, 0, 0, "Red");
    public static readonly LedColor Orange = new(255, 165, 0, "Orange");
    public static readonly LedColor Yellow = new(255, 255, 0, "Yellow");
    public static readonly LedColor Green = new(0, 255, 0, "Green");
    public static readonly LedColor Cyan = new(0, 255, 255, "Cyan");
    public static readonly LedColor Blue = new(0, 0, 255, "Blue");
    public static readonly LedColor Purple = new(128, 0, 128, "Purple");
    public static readonly LedColor Pink = new(255, 192, 203, "Pink");
    public static readonly LedColor White = new(255, 255, 255, "White");
    public static readonly LedColor Black = new(0, 0, 0, "Black/Off");
    
    /// <summary>Get all standard colors as an enumerable</summary>
    public static IEnumerable<LedColor> All => new[]
    {
        Red, Orange, Yellow, Green, Cyan, Blue, Purple, Pink, White, Black
    };
}

/// <summary>
/// Complete configuration for a single LED zone
/// </summary>
public record ZoneConfiguration
{
    public LedZone Zone { get; init; }
    public LedPattern Pattern { get; init; }
    public LedColor PrimaryColor { get; init; } = StandardColors.Red;
    public LedColor? SecondaryColor { get; init; }
    public LedColor? TertiaryColor { get; init; }
    public LedBrightness Brightness { get; init; } = LedBrightness.Maximum;
    public bool RainbowModeEnabled { get; init; }
    
    /// <summary>Create a simple solid color configuration</summary>
    public static ZoneConfiguration Solid(LedZone zone, LedColor color, LedBrightness? brightness = null)
    {
        return new ZoneConfiguration
        {
            Zone = zone,
            Pattern = LedPattern.Solid,
            PrimaryColor = color,
            Brightness = brightness ?? LedBrightness.Maximum,
            RainbowModeEnabled = false
        };
    }
    
    /// <summary>Create a rainbow configuration</summary>
    public static ZoneConfiguration Rainbow(LedZone zone, LedBrightness? brightness = null)
    {
        return new ZoneConfiguration
        {
            Zone = zone,
            Pattern = LedPattern.Rainbow,
            PrimaryColor = StandardColors.Red, // Placeholder
            Brightness = brightness ?? LedBrightness.Maximum,
            RainbowModeEnabled = true
        };
    }
    
    /// <summary>Create an off configuration</summary>
    public static ZoneConfiguration Off(LedZone zone)
    {
        return new ZoneConfiguration
        {
            Zone = zone,
            Pattern = LedPattern.Off,
            PrimaryColor = StandardColors.Black,
            Brightness = LedBrightness.Off,
            RainbowModeEnabled = false
        };
    }
}

// ============================================================================
// COMMANDS AND RESPONSES
// ============================================================================

/// <summary>
/// Base class for all LED commands sent via named pipes
/// </summary>
public abstract class LedCommand
{
    public Guid CommandId { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public abstract string CommandType { get; }
}

/// <summary>Set a single zone configuration</summary>
public class SetZoneCommand : LedCommand
{
    public override string CommandType => "SetZone";
    public ZoneConfiguration? Configuration { get; set; }
}

/// <summary>Turn off all zones</summary>
public class TurnOffAllCommand : LedCommand
{
    public override string CommandType => "TurnOffAll";
}

/// <summary>Turn on LEDs with initialization</summary>
public class TurnOnCommand : LedCommand
{
    public override string CommandType => "TurnOn";
}

/// <summary>Get current status of all zones</summary>
public class GetStatusCommand : LedCommand
{
    public override string CommandType => "GetStatus";
}

/// <summary>Connect to hardware</summary>
public class ConnectCommand : LedCommand
{
    public override string CommandType => "Connect";
    public string ComPort { get; set; } = "COM3";
}

/// <summary>Disconnect from hardware</summary>
public class DisconnectCommand : LedCommand
{
    public override string CommandType => "Disconnect";
}

/// <summary>
/// Standard response for all LED commands
/// </summary>
public class CommandResponse
{
    public Guid CommandId { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? ErrorDetails { get; set; }
    
    public static CommandResponse SuccessResult(Guid commandId, string message = "Command executed successfully")
    {
        return new CommandResponse
        {
            CommandId = commandId,
            Success = true,
            Message = message
        };
    }
    
    public static CommandResponse FailureResult(Guid commandId, string message, string? errorDetails = null)
    {
        return new CommandResponse
        {
            CommandId = commandId,
            Success = false,
            Message = message,
            ErrorDetails = errorDetails
        };
    }
}

/// <summary>Response containing current LED status</summary>
public class StatusResponse : CommandResponse
{
    public bool IsServiceConnected { get; set; }
    public bool IsHardwareConnected { get; set; }
    public string? ComPort { get; set; }
    public ZoneStatus[] ZoneStatuses { get; set; } = Array.Empty<ZoneStatus>();
}

/// <summary>Status of a single LED zone</summary>
public class ZoneStatus
{
    public LedZone Zone { get; set; }
    public LedPattern CurrentPattern { get; set; }
    public LedColor CurrentColor { get; set; } = StandardColors.Black;
    public LedBrightness CurrentBrightness { get; set; } = LedBrightness.Off;
    public bool RainbowModeEnabled { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

// ============================================================================
// NINLCS CLIENT - MAIN CLASS
// ============================================================================

/// <summary>
/// Client for communicating with the NINLCS LED Service
/// </summary>
public class NinlcsClient : IDisposable
{
    private NamedPipeClientStream? _pipeClient;
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
    private bool _disposed;

    /// <summary>Connect to the LED service</summary>
    public async Task<CommandResponse> ConnectToServiceAsync()
    {
        await _connectionSemaphore.WaitAsync();
        try
        {
            if (_pipeClient?.IsConnected == true)
            {
                return CommandResponse.SuccessResult(Guid.NewGuid(), "Already connected");
            }

            _pipeClient?.Dispose();
            _pipeClient = new NamedPipeClientStream(".", "NINLCS", PipeDirection.InOut);
            
            await _pipeClient.ConnectAsync(5000);
            
            return CommandResponse.SuccessResult(Guid.NewGuid(), "Connected to LED service");
        }
        catch (Exception ex)
        {
            return CommandResponse.FailureResult(Guid.NewGuid(), "Failed to connect to service", $"Exception: {ex.GetType().Name} - {ex.Message}");
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    /// <summary>Ensure connection to service</summary>
    private async Task EnsureConnectedAsync()
    {
        if (_pipeClient?.IsConnected != true)
        {
            var connectResult = await ConnectToServiceAsync();
            if (!connectResult.Success)
            {
                throw new InvalidOperationException($"Failed to connect to service: {connectResult.Message}");
            }
        }
    }

    /// <summary>Send a command to the service and get response</summary>
    private async Task<CommandResponse> SendCommandAsync(LedCommand command)
    {
        return await SendCommandAsync<CommandResponse>(command);
    }
    
    /// <summary>Send a command to the service and get response of specified type</summary>
    private async Task<T> SendCommandAsync<T>(LedCommand command) where T : CommandResponse
    {
        await _connectionSemaphore.WaitAsync();
        try
        {
            await EnsureConnectedAsync();
            
            // Serialize command
            var commandJson = JsonSerializer.Serialize(command, command.GetType(), new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            var commandBytes = System.Text.Encoding.UTF8.GetBytes(commandJson);
            
            // Write length prefix
            var lengthBytes = BitConverter.GetBytes(commandBytes.Length);
            await _pipeClient!.WriteAsync(lengthBytes, 0, lengthBytes.Length);
            
            // Write command
            await _pipeClient.WriteAsync(commandBytes, 0, commandBytes.Length);
            await _pipeClient.FlushAsync();
            
            // Read response length
            var responseLengthBytes = new byte[sizeof(int)];
            await _pipeClient.ReadExactlyAsync(responseLengthBytes, 0, responseLengthBytes.Length);
            var responseLength = BitConverter.ToInt32(responseLengthBytes, 0);
            
            // Read response
            var responseBytes = new byte[responseLength];
            await _pipeClient.ReadExactlyAsync(responseBytes, 0, responseLength);
            var responseJson = System.Text.Encoding.UTF8.GetString(responseBytes);
            
            // Deserialize response to the specific type
            var response = JsonSerializer.Deserialize<T>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            if (response == null)
            {
                if (typeof(T) == typeof(StatusResponse))
                {
                    var statusError = new StatusResponse
                    {
                        CommandId = command.CommandId,
                        Success = false,
                        Message = "Failed to deserialize response",
                        IsServiceConnected = false,
                        IsHardwareConnected = false,
                        ZoneStatuses = Array.Empty<ZoneStatus>()
                    };
                    return (T)(object)statusError;
                }
                else
                {
                    var errorResponse = CommandResponse.FailureResult(command.CommandId, "Failed to deserialize response");
                    return (T)(object)errorResponse;
                }
            }
            
            return response;
        }
        catch (Exception ex)
        {
            if (typeof(T) == typeof(StatusResponse))
            {
                var statusError = new StatusResponse
                {
                    CommandId = command.CommandId,
                    Success = false,
                    Message = "Communication error",
                    ErrorDetails = ex.Message,
                    IsServiceConnected = false,
                    IsHardwareConnected = false,
                    ZoneStatuses = Array.Empty<ZoneStatus>()
                };
                return (T)(object)statusError;
            }
            else
            {
                var errorResponse = CommandResponse.FailureResult(command.CommandId, "Communication error", ex.Message);
                return (T)(object)errorResponse;
            }
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    // ============================================================================
    // PUBLIC API METHODS - SAME AS CONSOLE APP
    // ============================================================================

    /// <summary>Set LED configuration for a specific zone using ZoneConfiguration</summary>
    public async Task<CommandResponse> SetZoneAsync(ZoneConfiguration config)
    {
        var command = new SetZoneCommand { Configuration = config };
        return await SendCommandAsync(command);
    }

    /// <summary>Set all zones to the same configuration</summary>
    public async Task<CommandResponse> SetAllZonesAsync(LedPattern pattern, 
        LedColor? primaryColor = null, LedColor? secondaryColor = null, LedColor? tertiaryColor = null,
        LedBrightness? brightness = null, bool rainbowMode = false)
    {
        var zones = Enum.GetValues<LedZone>();
        var tasks = zones.Select(zone => 
        {
            var config = new ZoneConfiguration
            {
                Zone = zone,
                Pattern = pattern,
                PrimaryColor = primaryColor ?? StandardColors.Red,
                SecondaryColor = secondaryColor,
                TertiaryColor = tertiaryColor,
                Brightness = brightness ?? LedBrightness.Maximum,
                RainbowModeEnabled = rainbowMode
            };
            return SetZoneAsync(config);
        });
        
        var results = await Task.WhenAll(tasks);
        
        var failedCount = results.Count(r => !r.Success);
        if (failedCount == 0)
        {
            return CommandResponse.SuccessResult(Guid.NewGuid(), $"All {zones.Length} zones configured successfully");
        }
        else
        {
            return CommandResponse.FailureResult(Guid.NewGuid(), $"{failedCount} of {zones.Length} zones failed to configure");
        }
    }

    /// <summary>Turn off all zones</summary>
    public async Task<CommandResponse> TurnOffAllAsync()
    {
        var command = new TurnOffAllCommand();
        return await SendCommandAsync(command);
    }
    
    /// <summary>Turn on all LEDs with default pattern</summary>
    public async Task<CommandResponse> TurnOnAsync()
    {
        var command = new TurnOnCommand();
        return await SendCommandAsync(command);
    }
    
    /// <summary>Disable rainbow mode on all LEDs</summary>
    public async Task<CommandResponse> DisableRainbowAsync()
    {
        return await SetAllZonesAsync(LedPattern.Solid, StandardColors.Blue, null, null, LedBrightness.Medium, false);
    }
    
    /// <summary>Get current LED status from service</summary>
    public async Task<StatusResponse> GetStatusAsync()
    {
        var command = new GetStatusCommand();
        return await SendCommandAsync<StatusResponse>(command);
    }
    
    /// <summary>Connect service to hardware</summary>
    public async Task<CommandResponse> ConnectToHardwareAsync(string comPort = "COM3")
    {
        var command = new ConnectCommand { ComPort = comPort };
        return await SendCommandAsync(command);
    }
    
    /// <summary>Disconnect service from hardware</summary>
    public async Task<CommandResponse> DisconnectFromHardwareAsync()
    {
        var command = new DisconnectCommand();
        return await SendCommandAsync(command);
    }

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
