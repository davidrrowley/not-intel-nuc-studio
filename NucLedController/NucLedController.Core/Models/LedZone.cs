namespace NucLedController.Core.Models;

/// <summary>
/// Represents the four LED zones on the Intel NUC
/// </summary>
public enum LedZone
{
    /// <summary>
    /// Zone A - Skull (logo header)
    /// </summary>
    Skull = 0,
    
    /// <summary>
    /// Zone B - Bottom Left
    /// </summary>
    BottomLeft = 1,
    
    /// <summary>
    /// Zone C - Bottom Right
    /// </summary>
    BottomRight = 2,
    
    /// <summary>
    /// Zone D - Front Bottom
    /// </summary>
    FrontBottom = 3
}
