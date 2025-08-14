using NucLedController.Core.Models;
using NucLedController.Core.Services;

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
    /// Gets whether the LEDs are currently enabled/on
    /// </summary>
    bool LedsEnabled { get; }
    
    /// <summary>
    /// Gets the current connection status message
    /// </summary>
    string ConnectionStatus { get; }
    
    /// <summary>
    /// Gets the state manager for LED configuration persistence
    /// </summary>
    LedStateManager StateManager { get; }
    
    /// <summary>
    /// Initialize the controller and load saved state
    /// </summary>
    Task InitializeAsync();
    
    /// <summary>
    /// Event raised when connection status changes
    /// </summary>
    event EventHandler<bool> ConnectionChanged;
    
    /// <summary>
    /// Event raised when status message changes
    /// </summary>
    event EventHandler<string> StatusChanged;
    
    /// <summary>
    /// Event raised when LED state changes (on/off)
    /// </summary>
    event EventHandler<bool> LedStateChanged;
    
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
    
    /// <summary>
    /// Get current LED patterns from hardware (Intel SDK style)
    /// </summary>
    Task<LedStatus?> GetCurrentPatternsAsync();
    
    /// <summary>
    /// Update application state from hardware state
    /// </summary>
    Task<LedCommandResult> UpdateFromDeviceAsync();
    
    /// <summary>
    /// Enhanced GetCurrentPattern method like Intel SDK
    /// </summary>
    Task<LedCommandResult> GetCurrentPatternAsync();
}
