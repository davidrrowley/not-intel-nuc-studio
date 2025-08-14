namespace NucLedController.Core.Models;

/// <summary>
/// Common LED color constants that work well with Intel NUC
/// </summary>
public static class LedColors
{
    public const int Red = 16711680;      // 0xFF0000 - pure red
    public const int Green = 65280;       // 0x00FF00 - pure green  
    public const int Blue = 255;          // 0x0000FF - pure blue
    public const int Yellow = 16776960;   // 0xFFFF00 - red + green
    public const int Purple = 16711935;   // 0xFF00FF - red + blue
    public const int Cyan = 65535;        // 0x00FFFF - green + blue
    public const int White = 16777215;    // 0xFFFFFF - all colors
    public const int Orange = 16753920;   // 0xFF8000 - red + half green
    public const int Black = 0;           // 0x000000 - no color (off)
    public const int Off = 0;             // Alias for Black
    
    /// <summary>
    /// Gets a user-friendly name for a color value
    /// </summary>
    public static string GetColorName(int colorValue)
    {
        return colorValue switch
        {
            Red => "Red",
            Green => "Green", 
            Blue => "Blue",
            Yellow => "Yellow",
            Purple => "Purple",
            Cyan => "Cyan",
            White => "White",
            Orange => "Orange",
            Black => "Off",
            _ => $"Custom ({colorValue})"
        };
    }
}
