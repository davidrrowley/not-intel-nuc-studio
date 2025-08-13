namespace NucLedController.Core.Models;

/// <summary>
/// Common LED color constants that work well with Intel NUC
/// </summary>
public static class LedColors
{
    public const int Red = 0;
    public const int Green = 96;
    public const int Blue = 160;
    public const int Yellow = 70;
    public const int Purple = 200;
    public const int Cyan = 128;
    public const int White = 255;
    public const int Off = 0;
    
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
            _ => $"Custom ({colorValue})"
        };
    }
}
