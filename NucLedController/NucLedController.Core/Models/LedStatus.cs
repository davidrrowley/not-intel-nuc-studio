using NucLedController.Core.Models;

namespace NucLedController.Core.Models;

/// <summary>
/// Represents the current LED status from hardware
/// Based on Intel SDK LedStatus structure
/// </summary>
public class LedStatus
{
    public bool ButtonStatus { get; set; }
    public HardwareLedPattern[] LedPatternList { get; set; } = Array.Empty<HardwareLedPattern>();
    
    public LedStatus()
    {
    }
    
    public LedStatus(bool buttonStatus, HardwareLedPattern[] patterns)
    {
        ButtonStatus = buttonStatus;
        LedPatternList = patterns ?? Array.Empty<HardwareLedPattern>();
    }
}

/// <summary>
/// Represents a single LED pattern for a zone
/// Based on Intel SDK LedPattern structure
/// </summary>
public class HardwareLedPattern
{
    public byte Channel { get; set; }  // Zone identifier (A=65, B=66, C=67, D=68)
    public int Color { get; set; }     // RGB color value
    public int Brightness { get; set; } // Brightness level (0-5)
    public int Pattern { get; set; }   // Pattern type (1=static, etc.)
    public int Speed { get; set; }     // Animation speed (0-5)
    public bool Enabled { get; set; }  // Zone enabled state
    
    public HardwareLedPattern()
    {
    }
    
    public HardwareLedPattern(byte channel, int color, int brightness = 5, int pattern = 1, int speed = 3, bool enabled = true)
    {
        Channel = channel;
        Color = color;
        Brightness = brightness;
        Pattern = pattern;
        Speed = speed;
        Enabled = enabled;
    }
    
    /// <summary>
    /// Convert zone to channel byte (A=65, B=66, C=67, D=68)
    /// </summary>
    public static byte ZoneToChannel(LedZone zone)
    {
        return zone switch
        {
            LedZone.Skull => 65,      // 'A'
            LedZone.BottomLeft => 66, // 'B'
            LedZone.BottomRight => 67,// 'C'
            LedZone.FrontBottom => 68,// 'D'
            _ => 65
        };
    }
    
    /// <summary>
    /// Convert channel byte to zone
    /// </summary>
    public static LedZone ChannelToZone(byte channel)
    {
        return channel switch
        {
            65 => LedZone.Skull,      // 'A'
            66 => LedZone.BottomLeft, // 'B'
            67 => LedZone.BottomRight,// 'C'
            68 => LedZone.FrontBottom,// 'D'
            _ => LedZone.Skull
        };
    }
}
