using System.Text.Json;
using NucLedController.Core.Models;

namespace NucLedController.Core.Services;

/// <summary>
/// Manages LED state persistence and synchronization
/// Handles saving/loading state and keeping UI in sync with hardware
/// </summary>
public class LedStateManager
{
    private static readonly string StateFileName = "led_state.json";
    private static readonly string StateDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NucLedController"
    );
    private static readonly string StateFilePath = Path.Combine(StateDirectory, StateFileName);
    
    private LedState _currentState;
    private readonly object _stateLock = new object();
    
    public event EventHandler<LedState>? StateChanged;
    public event EventHandler<ZoneState>? ZoneStateChanged;
    public event EventHandler<bool>? LedsEnabledChanged;
    public event EventHandler<bool>? EffectsEnabledChanged;
    
    public LedState CurrentState 
    { 
        get 
        { 
            lock (_stateLock) 
            { 
                return _currentState.Clone(); 
            } 
        } 
    }
    
    public LedStateManager()
    {
        _currentState = new LedState();
        EnsureStateDirectoryExists();
    }
    
    /// <summary>
    /// Load state from persistent storage
    /// </summary>
    public async Task<bool> LoadStateAsync()
    {
        try
        {
            if (!File.Exists(StateFilePath))
            {
                // No saved state, use defaults
                await SaveStateAsync();
                return true;
            }
            
            var json = await File.ReadAllTextAsync(StateFilePath);
            var loadedState = JsonSerializer.Deserialize<LedState>(json);
            
            if (loadedState != null)
            {
                lock (_stateLock)
                {
                    _currentState = loadedState;
                }
                
                // Notify subscribers of loaded state
                StateChanged?.Invoke(this, _currentState.Clone());
                LedsEnabledChanged?.Invoke(this, _currentState.LedsEnabled);
                EffectsEnabledChanged?.Invoke(this, _currentState.EffectsEnabled);
                
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load LED state: {ex.Message}");
        }
        
        // Fallback to defaults
        lock (_stateLock)
        {
            _currentState = new LedState();
        }
        await SaveStateAsync();
        return false;
    }
    
    /// <summary>
    /// Save current state to persistent storage
    /// </summary>
    public async Task SaveStateAsync()
    {
        try
        {
            lock (_stateLock)
            {
                _currentState.LastUpdated = DateTime.Now;
            }
            
            var json = JsonSerializer.Serialize(_currentState, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(StateFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save LED state: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Update the overall LED enabled state
    /// </summary>
    public async Task SetLedsEnabledAsync(bool enabled)
    {
        bool changed = false;
        lock (_stateLock)
        {
            if (_currentState.LedsEnabled != enabled)
            {
                _currentState.LedsEnabled = enabled;
                changed = true;
            }
        }
        
        if (changed)
        {
            await SaveStateAsync();
            LedsEnabledChanged?.Invoke(this, enabled);
            StateChanged?.Invoke(this, _currentState.Clone());
        }
    }
    
    /// <summary>
    /// Update the effects enabled state
    /// </summary>
    public async Task SetEffectsEnabledAsync(bool enabled)
    {
        bool changed = false;
        lock (_stateLock)
        {
            if (_currentState.EffectsEnabled != enabled)
            {
                _currentState.EffectsEnabled = enabled;
                changed = true;
            }
        }
        
        if (changed)
        {
            await SaveStateAsync();
            EffectsEnabledChanged?.Invoke(this, enabled);
            StateChanged?.Invoke(this, _currentState.Clone());
        }
    }
    
    /// <summary>
    /// Update a specific zone's state
    /// </summary>
    public async Task SetZoneStateAsync(LedZone zone, int color, int brightness = 5, LedPattern pattern = LedPattern.Static, bool enabled = true)
    {
        bool changed = false;
        ZoneState? updatedZone = null;
        
        lock (_stateLock)
        {
            if (_currentState.Zones.TryGetValue(zone, out var zoneState))
            {
                if (zoneState.Color != color || 
                    zoneState.Brightness != brightness || 
                    zoneState.Pattern != pattern || 
                    zoneState.Enabled != enabled)
                {
                    zoneState.Color = color;
                    zoneState.Brightness = brightness;
                    zoneState.Pattern = pattern;
                    zoneState.Enabled = enabled;
                    zoneState.LastChanged = DateTime.Now;
                    updatedZone = zoneState.Clone();
                    changed = true;
                }
            }
        }
        
        if (changed && updatedZone != null)
        {
            await SaveStateAsync();
            ZoneStateChanged?.Invoke(this, updatedZone);
            StateChanged?.Invoke(this, _currentState.Clone());
        }
    }
    
    /// <summary>
    /// Set all zones to the same state
    /// </summary>
    public async Task SetAllZonesAsync(int color, int brightness = 5, LedPattern pattern = LedPattern.Static, bool enabled = true)
    {
        bool changed = false;
        var updatedZones = new List<ZoneState>();
        
        lock (_stateLock)
        {
            foreach (var zone in Enum.GetValues<LedZone>())
            {
                if (_currentState.Zones.TryGetValue(zone, out var zoneState))
                {
                    if (zoneState.Color != color || 
                        zoneState.Brightness != brightness || 
                        zoneState.Pattern != pattern || 
                        zoneState.Enabled != enabled)
                    {
                        zoneState.Color = color;
                        zoneState.Brightness = brightness;
                        zoneState.Pattern = pattern;
                        zoneState.Enabled = enabled;
                        zoneState.LastChanged = DateTime.Now;
                        updatedZones.Add(zoneState.Clone());
                        changed = true;
                    }
                }
            }
        }
        
        if (changed)
        {
            await SaveStateAsync();
            foreach (var zone in updatedZones)
            {
                ZoneStateChanged?.Invoke(this, zone);
            }
            StateChanged?.Invoke(this, _currentState.Clone());
        }
    }
    
    /// <summary>
    /// Reset all zones to off state
    /// </summary>
    public async Task ResetAllZonesAsync()
    {
        await SetAllZonesAsync(LedColors.Black, 5, LedPattern.Static, false);
    }
    
    /// <summary>
    /// Get the state of a specific zone
    /// </summary>
    public ZoneState? GetZoneState(LedZone zone)
    {
        lock (_stateLock)
        {
            return _currentState.Zones.TryGetValue(zone, out var zoneState) 
                ? zoneState.Clone() 
                : null;
        }
    }
    
    /// <summary>
    /// Get a description of the current state
    /// </summary>
    public string GetStateDescription()
    {
        lock (_stateLock)
        {
            return _currentState.GetDescription();
        }
    }
    
    /// <summary>
    /// Check if the current state indicates LEDs should be completely off
    /// </summary>
    public bool IsCompletelyOff()
    {
        lock (_stateLock)
        {
            return _currentState.IsCompletelyOff();
        }
    }
    
    /// <summary>
    /// Import state from another LedState object
    /// </summary>
    public async Task ImportStateAsync(LedState state)
    {
        lock (_stateLock)
        {
            _currentState = state.Clone();
        }
        
        await SaveStateAsync();
        StateChanged?.Invoke(this, _currentState.Clone());
        LedsEnabledChanged?.Invoke(this, _currentState.LedsEnabled);
        EffectsEnabledChanged?.Invoke(this, _currentState.EffectsEnabled);
    }
    
    private void EnsureStateDirectoryExists()
    {
        try
        {
            if (!Directory.Exists(StateDirectory))
            {
                Directory.CreateDirectory(StateDirectory);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create state directory: {ex.Message}");
        }
    }
}
