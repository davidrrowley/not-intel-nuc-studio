using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using NucLedController.Core.Models;

namespace NucLedController.Client;

/// <summary>
/// Service command types (copied from service for consistency)
/// </summary>
public enum ServiceCommand
{
    TurnOn,
    TurnOff,
    GetStatus,
    GetCurrentPatterns,
    SetColor,
    Ping
}

/// <summary>
/// Service request structure (copied from service)
/// </summary>
public class ServiceRequest
{
    public ServiceCommand Command { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Service response structure (copied from service)
/// </summary>
public class ServiceResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object>? Data { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

/// <summary>
/// Client for communicating with the NUC LED Controller Service
/// </summary>
public class NucLedServiceClient : IDisposable
{
    private readonly string _pipeName = "NucLedController";
    private readonly int _timeoutMs;

    public NucLedServiceClient(int timeoutMs = 30000) // Increased from 5 seconds to 30 seconds
    {
        _timeoutMs = timeoutMs;
    }

    /// <summary>
    /// Turn the LEDs on
    /// </summary>
    public async Task<LedCommandResult> TurnOnAsync()
    {
        var response = await SendRequestAsync(new ServiceRequest { Command = ServiceCommand.TurnOn });
        return response.Success 
            ? LedCommandResult.SuccessResult(response.Message)
            : LedCommandResult.FailureResult(response.Message);
    }

    /// <summary>
    /// Turn the LEDs off
    /// </summary>
    public async Task<LedCommandResult> TurnOffAsync()
    {
        var response = await SendRequestAsync(new ServiceRequest { Command = ServiceCommand.TurnOff });
        return response.Success 
            ? LedCommandResult.SuccessResult(response.Message)
            : LedCommandResult.FailureResult(response.Message);
    }

    /// <summary>
    /// Get current LED status and patterns
    /// </summary>
    public async Task<(bool success, LedStatus? status, string message)> GetStatusAsync()
    {
        var response = await SendRequestAsync(new ServiceRequest { Command = ServiceCommand.GetStatus });
        
        LedStatus? status = null;
        if (response.Success && response.Data?.TryGetValue("patterns", out var patternsObj) == true)
        {
            if (patternsObj is JsonElement element)
            {
                status = JsonSerializer.Deserialize<LedStatus>(element.GetRawText());
            }
        }

        return (response.Success, status, response.Message);
    }

    /// <summary>
    /// Get current LED patterns from hardware
    /// </summary>
    public async Task<(bool success, LedStatus? patterns, string message)> GetCurrentPatternsAsync()
    {
        var response = await SendRequestAsync(new ServiceRequest { Command = ServiceCommand.GetCurrentPatterns });
        
        LedStatus? patterns = null;
        if (response.Success && response.Data?.TryGetValue("patterns", out var patternsObj) == true)
        {
            if (patternsObj is JsonElement element)
            {
                patterns = JsonSerializer.Deserialize<LedStatus>(element.GetRawText());
            }
        }

        return (response.Success, patterns, response.Message);
    }

    /// <summary>
    /// Set LED color for a specific zone
    /// </summary>
    public async Task<LedCommandResult> SetZoneColorAsync(LedZone zone, int colorValue)
    {
        var request = new ServiceRequest 
        { 
            Command = ServiceCommand.SetColor,
            Parameters = new Dictionary<string, object>
            {
                ["zone"] = (int)zone,
                ["color"] = colorValue
            }
        };

        var response = await SendRequestAsync(request);
        return response.Success 
            ? LedCommandResult.SuccessResult(response.Message)
            : LedCommandResult.FailureResult(response.Message);
    }

    /// <summary>
    /// Ping the service to check if it's running
    /// </summary>
    public async Task<(bool success, bool connected, string message)> PingAsync()
    {
        try
        {
            var response = await SendRequestAsync(new ServiceRequest { Command = ServiceCommand.Ping });
            
            bool connected = false;
            if (response.Success && response.Data?.TryGetValue("connected", out var connectedObj) == true)
            {
                if (connectedObj is JsonElement element)
                {
                    connected = element.GetBoolean();
                }
                else
                {
                    connected = Convert.ToBoolean(connectedObj);
                }
            }

            return (response.Success, connected, response.Message);
        }
        catch (Exception ex)
        {
            return (false, false, $"Service not available: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if the service is running and accessible
    /// </summary>
    public async Task<bool> IsServiceRunningAsync()
    {
        var (success, _, _) = await PingAsync();
        return success;
    }

    private async Task<ServiceResponse> SendRequestAsync(ServiceRequest request)
    {
        using var pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
        
        // Connect to the service with timeout
        using var cts = new CancellationTokenSource(_timeoutMs);
        await pipeClient.ConnectAsync(cts.Token);

        // Send request
        var requestJson = JsonSerializer.Serialize(request);
        var requestBytes = Encoding.UTF8.GetBytes(requestJson);
        await pipeClient.WriteAsync(requestBytes, cts.Token);
        await pipeClient.FlushAsync();

        // Read response
        var buffer = new byte[4096];
        var bytesRead = await pipeClient.ReadAsync(buffer, cts.Token);
        var responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        var response = JsonSerializer.Deserialize<ServiceResponse>(responseJson);
        return response ?? new ServiceResponse
        {
            Success = false,
            Message = "Failed to deserialize response"
        };
    }

    public void Dispose()
    {
        // Nothing to dispose currently
    }
}
