using System.IO.Pipes;
using System.Text.Json;

/// <summary>
/// Raw service client that communicates directly with NINLCS service using only JSON and named pipes.
/// No project references needed - completely self-contained.
/// </summary>
public class RawNinlcsClient : IDisposable
{
    private NamedPipeClientStream? _pipeClient;
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Connect to the NINLCS service
    /// </summary>
    public async Task<ServiceResponse> ConnectAsync()
    {
        await _connectionSemaphore.WaitAsync();
        try
        {
            if (_pipeClient?.IsConnected == true)
            {
                return new ServiceResponse { Success = true, Message = "Already connected" };
            }

            _pipeClient?.Dispose();
            _pipeClient = new NamedPipeClientStream(".", "NINLCS", PipeDirection.InOut);
            
            await _pipeClient.ConnectAsync(5000);
            
            return new ServiceResponse { Success = true, Message = "Connected to NINLCS service" };
        }
        catch (Exception ex)
        {
            return new ServiceResponse { Success = false, Message = $"Connection failed: {ex.Message}" };
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    /// <summary>
    /// Send a command to the service
    /// </summary>
    private async Task<ServiceResponse> SendCommandAsync(object command)
    {
        await _connectionSemaphore.WaitAsync();
        try
        {
            if (_pipeClient?.IsConnected != true)
            {
                var connectResult = await ConnectAsync();
                if (!connectResult.Success)
                {
                    return connectResult;
                }
            }
            
            // Serialize command to JSON
            var commandJson = JsonSerializer.Serialize(command, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            var commandBytes = System.Text.Encoding.UTF8.GetBytes(commandJson);
            
            // Write length prefix + command
            var lengthBytes = BitConverter.GetBytes(commandBytes.Length);
            await _pipeClient!.WriteAsync(lengthBytes, 0, lengthBytes.Length);
            await _pipeClient.WriteAsync(commandBytes, 0, commandBytes.Length);
            await _pipeClient.FlushAsync();
            
            // Read response length
            var responseLengthBytes = new byte[sizeof(int)];
            await _pipeClient.ReadExactlyAsync(responseLengthBytes, 0, responseLengthBytes.Length);
            var responseLength = BitConverter.ToInt32(responseLengthBytes, 0);
            
            // Read response
            var responseBytes = new byte[responseLength];
            await _pipeClient.ReadExactlyAsync(responseBytes, 0, responseLength);
            var responseJson = System.Text.Encoding.UTF8.GetString(responseBytes);
            
            // Deserialize response
            var response = JsonSerializer.Deserialize<ServiceResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            return response ?? new ServiceResponse { Success = false, Message = "Failed to parse response" };
        }
        catch (Exception ex)
        {
            return new ServiceResponse { Success = false, Message = $"Communication error: {ex.Message}" };
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    /// <summary>
    /// Set LED zone to solid color
    /// </summary>
    public async Task<ServiceResponse> SetZoneSolidAsync(string zone, string color, int brightness = 100)
    {
        var command = new
        {
            Type = "SetZone",
            CommandId = Guid.NewGuid(),
            Configuration = new
            {
                Zone = zone,
                Pattern = "Solid",
                PrimaryColor = new { Name = color, R = GetColorR(color), G = GetColorG(color), B = GetColorB(color) },
                Brightness = new { Percentage = brightness },
                RainbowModeEnabled = false
            }
        };

        return await SendCommandAsync(command);
    }

    /// <summary>
    /// Set all zones to same color/pattern
    /// </summary>
    public async Task<ServiceResponse> SetAllZonesAsync(string pattern, string color, int brightness = 100, bool rainbowMode = false)
    {
        var zones = new[] { "Skull", "BottomLeft", "BottomRight", "BottomFront" };
        var results = new List<ServiceResponse>();

        foreach (var zone in zones)
        {
            var command = new
            {
                Type = "SetZone",
                CommandId = Guid.NewGuid(),
                Configuration = new
                {
                    Zone = zone,
                    Pattern = pattern,
                    PrimaryColor = new { Name = color, R = GetColorR(color), G = GetColorG(color), B = GetColorB(color) },
                    Brightness = new { Percentage = brightness },
                    RainbowModeEnabled = rainbowMode
                }
            };

            var result = await SendCommandAsync(command);
            results.Add(result);
        }

        var failedCount = results.Count(r => !r.Success);
        if (failedCount == 0)
        {
            return new ServiceResponse { Success = true, Message = "All zones set successfully" };
        }
        else
        {
            return new ServiceResponse { Success = false, Message = $"{failedCount} zones failed" };
        }
    }

    /// <summary>
    /// Turn off all LEDs
    /// </summary>
    public async Task<ServiceResponse> TurnOffAllAsync()
    {
        var command = new
        {
            Type = "TurnOffAll",
            CommandId = Guid.NewGuid()
        };

        return await SendCommandAsync(command);
    }

    /// <summary>
    /// Enable rainbow mode on all zones
    /// </summary>
    public async Task<ServiceResponse> SetRainbowModeAsync(int brightness = 100)
    {
        return await SetAllZonesAsync("Rainbow", "Red", brightness, true);
    }

    /// <summary>
    /// Get current LED status
    /// </summary>
    public async Task<ServiceResponse> GetStatusAsync()
    {
        var command = new
        {
            Type = "GetStatus",
            CommandId = Guid.NewGuid()
        };

        return await SendCommandAsync(command);
    }

    // Helper methods for color mapping
    private static int GetColorR(string color) => color.ToLower() switch
    {
        "red" => 255, "green" => 0, "blue" => 0, "yellow" => 255,
        "purple" => 255, "cyan" => 0, "white" => 255, "black" => 0,
        _ => 255
    };

    private static int GetColorG(string color) => color.ToLower() switch
    {
        "red" => 0, "green" => 255, "blue" => 0, "yellow" => 255,
        "purple" => 0, "cyan" => 255, "white" => 255, "black" => 0,
        _ => 0
    };

    private static int GetColorB(string color) => color.ToLower() switch
    {
        "red" => 0, "green" => 0, "blue" => 255, "yellow" => 0,
        "purple" => 255, "cyan" => 255, "white" => 255, "black" => 0,
        _ => 0
    };

    public void Dispose()
    {
        if (!_disposed)
        {
            _pipeClient?.Dispose();
            _connectionSemaphore.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Simple response class for service communication
/// </summary>
public class ServiceResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? ErrorDetails { get; set; }
}
