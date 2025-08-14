using System.Text.Json.Serialization;

namespace NucLedController.Core.Models;

/// <summary>
/// Represents the complete LED configuration state
/// Used for persistence and state management
/// </summary>
public class LedState
{
    [JsonPropertyName("ledsEnabled")]
    public bool LedsEnabled { get; set; } = false;
    
    [JsonPropertyName("effectsEnabled")]
    public bool EffectsEnabled { get; set; } = false;
    
    [JsonPropertyName("zones")]
    public Dictionary<LedZone, ZoneState> Zones { get; set; } = new();
    
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.Now;
    
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";
    
    public LedState()
    {
        // Initialize with default zone states
        foreach (LedZone zone in Enum.GetValues<LedZone>())
        {
            Zones[zone] = new ZoneState
            {
                Zone = zone,
                Color = LedColors.Black, // Default to off
                Brightness = 5,
                Pattern = LedPattern.Static,
                Enabled = false
            };
        }
    }
    
    /// <summary>
    /// Create a copy of the current state
    /// </summary>
    public LedState Clone()
    {
        var clone = new LedState
        {
            LedsEnabled = this.LedsEnabled,
            EffectsEnabled = this.EffectsEnabled,
            LastUpdated = this.LastUpdated,
            Version = this.Version
        };
        
        foreach (var kvp in this.Zones)
        {
            clone.Zones[kvp.Key] = kvp.Value.Clone();
        }
        
        return clone;
    }
    
    /// <summary>
    /// Check if this state represents LEDs being completely off
    /// </summary>
    public bool IsCompletelyOff()
    {
        return !LedsEnabled || Zones.Values.All(z => !z.Enabled || z.Color == LedColors.Black);
    }
    
    /// <summary>
    /// Get a simple description of the current state
    /// </summary>
    public string GetDescription()
    {
        if (!LedsEnabled)
            return "LEDs Off";
            
        if (EffectsEnabled)
            return "Effects On";
            
        var enabledZones = Zones.Values.Count(z => z.Enabled && z.Color != LedColors.Black);
        return enabledZones switch
        {
            0 => "All Zones Dark",
            1 => "1 Zone Active",
            _ => $"{enabledZones} Zones Active"
        };
    }
}

/// <summary>
/// Represents the state of a single LED zone
/// </summary>
public class ZoneState
{
    [JsonPropertyName("zone")]
    public LedZone Zone { get; set; }
    
    [JsonPropertyName("color")]
    public int Color { get; set; } = LedColors.Black;
    
    [JsonPropertyName("brightness")]
    public int Brightness { get; set; } = 5;
    
    [JsonPropertyName("pattern")]
    public LedPattern Pattern { get; set; } = LedPattern.Static;
    
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;
    
    [JsonPropertyName("lastChanged")]
    public DateTime LastChanged { get; set; } = DateTime.Now;
    
    public ZoneState Clone()
    {
        return new ZoneState
        {
            Zone = this.Zone,
            Color = this.Color,
            Brightness = this.Brightness,
            Pattern = this.Pattern,
            Enabled = this.Enabled,
            LastChanged = this.LastChanged
        };
    }
    
    /// <summary>
    /// Get a color name if it matches a known color
    /// </summary>
    public string GetColorName()
    {
        return Color switch
        {
            LedColors.Red => "Red",
            LedColors.Green => "Green",
            LedColors.Blue => "Blue",
            LedColors.Yellow => "Yellow",
            LedColors.White => "White",
            LedColors.Purple => "Purple",
            LedColors.Cyan => "Cyan",
            LedColors.Orange => "Orange",
            LedColors.Black => "Off",
            _ => $"Custom ({Color})"
        };
    }
}

/// <summary>
/// LED patterns supported by the hardware
/// </summary>
public enum LedPattern
{
    Static = 1,
    Breathing = 2,
    Pulsing = 3,
    Rainbow = 4
}
