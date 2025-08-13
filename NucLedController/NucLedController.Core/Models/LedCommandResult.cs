namespace NucLedController.Core.Models;

/// <summary>
/// Result of an LED command operation
/// </summary>
public class LedCommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    
    public static LedCommandResult SuccessResult(string message = "Command executed successfully")
        => new() { Success = true, Message = message };
        
    public static LedCommandResult FailureResult(string message, Exception? exception = null)
        => new() { Success = false, Message = message, Exception = exception };
}
