using NucLedController.Core.Models;

namespace NucLedController.Core.Interfaces;

/// <summary>
/// Interface for NUC LED controller operations
/// </summary>
public interface INucLedController : IDisposable
{
    /// <summary>
    /// Gets whether the controller is connected to the NUC
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// Gets the current connection status message
    /// </summary>
    string ConnectionStatus { get; }
    
    /// <summary>
    /// Event raised when connection status changes
    /// </summary>
    event EventHandler<bool> ConnectionChanged;
    
    /// <summary>
    /// Event raised when status message changes
    /// </summary>
    event EventHandler<string> StatusChanged;
    
    /// <summary>
    /// Connect to the NUC LED controller
    /// </summary>
    Task<LedCommandResult> ConnectAsync(string portName = "COM3");
    
    /// <summary>
    /// Disconnect from the NUC LED controller
    /// </summary>
    Task<LedCommandResult> DisconnectAsync();
    
    /// <summary>
    /// Set color for a specific LED zone
    /// </summary>
    Task<LedCommandResult> SetZoneColorAsync(LedZone zone, int color, int brightness = 5);
    
    /// <summary>
    /// Set the same color for all LED zones
    /// </summary>
    Task<LedCommandResult> SetAllZonesAsync(int color, int brightness = 5);
    
    /// <summary>
    /// Disable rainbow mode on all zones
    /// </summary>
    Task<LedCommandResult> DisableRainbowAsync();
    
    /// <summary>
    /// Turn off all LEDs
    /// </summary>
    Task<LedCommandResult> TurnOffAsync();
    
    /// <summary>
    /// Turn on all LEDs with default settings
    /// </summary>
    Task<LedCommandResult> TurnOnAsync();
}
