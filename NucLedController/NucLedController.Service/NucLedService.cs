using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using NucLedController.Core;
using NucLedController.Core.Models;

namespace NucLedController.Service;

/// <summary>
/// Service command types
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
/// Service request structure
/// </summary>
public class ServiceRequest
{
    public ServiceCommand Command { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Service response structure
/// </summary>
public class ServiceResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object>? Data { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

/// <summary>
/// Windows Service that exclusively manages the NUC LED COM port
/// </summary>
public class NucLedService : BackgroundService
{
    private readonly ILogger<NucLedService> _logger;
    private readonly string _pipeName = "NucLedController";
    private NucLedController.Core.NucLedController? _ledController;
    private readonly SemaphoreSlim _operationSemaphore = new(1, 1);

    public NucLedService(ILogger<NucLedService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 NUC LED Controller Service starting...");

        // Initialize LED controller - this service owns the COM port exclusively
        try
        {
            _ledController = new NucLedController.Core.NucLedController();
            
            // Service should own COM port exclusively - no mutex needed since we're the only user
            _logger.LogInformation("🔐 Service is the exclusive COM port owner - connecting directly...");
            
            // Try connecting without the complex mutex logic since we're the service
            var result = await _ledController.ConnectAsync();
            
            if (result.Success)
            {
                _logger.LogInformation("🔌 Service successfully connected to NUC LED hardware");
            }
            else
            {
                _logger.LogWarning("⚠️ Service failed to connect to NUC LED hardware: {Message}", result.Message);
                _logger.LogInformation("📡 Service will continue running and retry connection on first client request");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Service failed to initialize LED controller");
            _logger.LogInformation("📡 Service will continue running and retry connection on first client request");
        }

        // Start named pipe server to handle client requests
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pipeServer = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous);

                _logger.LogDebug("📡 Waiting for client connection...");
                await pipeServer.WaitForConnectionAsync(stoppingToken);
                _logger.LogDebug("✅ Client connected");

                // Handle the client request synchronously to avoid disposal issues
                await HandleClientAsync(pipeServer);
                
                // Dispose the pipe after handling is complete
                await pipeServer.DisposeAsync();
            }
            catch (OperationCanceledException)
            {
                // Service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error in pipe server loop");
                await Task.Delay(1000, stoppingToken); // Brief delay before retrying
            }
        }

        _logger.LogInformation("🛑 NUC LED Controller Service stopping...");
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipeServer)
    {
        try
        {
            // Read request
            var buffer = new byte[4096];
            var bytesRead = await pipeServer.ReadAsync(buffer);
            var requestJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            
            _logger.LogDebug("📥 Received request: {Request}", requestJson);

            var request = JsonSerializer.Deserialize<ServiceRequest>(requestJson);
            if (request == null)
            {
                await SendResponseAsync(pipeServer, new ServiceResponse
                {
                    Success = false,
                    Message = "Invalid request format"
                });
                return;
            }

            // Process the request
            var response = await ProcessRequestAsync(request);

            // Send response
            await SendResponseAsync(pipeServer, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error handling client request");
            try
            {
                await SendResponseAsync(pipeServer, new ServiceResponse
                {
                    Success = false,
                    Message = $"Server error: {ex.Message}"
                });
            }
            catch
            {
                // If we can't send the error response, just log it
                _logger.LogError("💥 Failed to send error response to client");
            }
        }
    }

    private async Task<ServiceResponse> ProcessRequestAsync(ServiceRequest request)
    {
        await _operationSemaphore.WaitAsync();
        try
        {
            _logger.LogInformation("🎯 Processing command: {Command}", request.Command);

            if (_ledController == null)
            {
                return new ServiceResponse
                {
                    Success = false,
                    Message = "LED controller not initialized",
                    RequestId = request.RequestId
                };
            }

            return request.Command switch
            {
                ServiceCommand.TurnOn => await HandleTurnOnAsync(request),
                ServiceCommand.TurnOff => await HandleTurnOffAsync(request),
                ServiceCommand.GetStatus => await HandleGetStatusAsync(request),
                ServiceCommand.GetCurrentPatterns => await HandleGetCurrentPatternsAsync(request),
                ServiceCommand.SetColor => await HandleSetColorAsync(request),
                ServiceCommand.Ping => new ServiceResponse
                {
                    Success = true,
                    Message = "Service is running",
                    RequestId = request.RequestId,
                    Data = new Dictionary<string, object>
                    {
                        ["connected"] = _ledController.IsConnected,
                        ["timestamp"] = DateTimeOffset.UtcNow.ToString("O")
                    }
                },
                _ => new ServiceResponse
                {
                    Success = false,
                    Message = $"Unknown command: {request.Command}",
                    RequestId = request.RequestId
                }
            };
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    private async Task<ServiceResponse> HandleTurnOnAsync(ServiceRequest request)
    {
        try
        {
            if (!_ledController!.IsConnected)
            {
                var connectResult = await _ledController.ConnectAsync();
                if (!connectResult.Success)
                {
                    return new ServiceResponse
                    {
                        Success = false,
                        Message = $"Failed to connect: {connectResult.Message}",
                        RequestId = request.RequestId
                    };
                }
            }

            // Start the turn-on operation in background and return immediately
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await _ledController.TurnOnAsync();
                    _logger.LogInformation("🟢 Background Turn ON completed: {Success} - {Message}", result.Success, result.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "💥 Background Turn ON failed");
                }
            });

            _logger.LogInformation("🟢 Turn ON command accepted - executing in background");

            return new ServiceResponse
            {
                Success = true,
                Message = "Turn ON command accepted - executing in background",
                RequestId = request.RequestId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error starting turn ON command");
            return new ServiceResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                RequestId = request.RequestId
            };
        }
    }

    private async Task<ServiceResponse> HandleTurnOffAsync(ServiceRequest request)
    {
        try
        {
            if (!_ledController!.IsConnected)
            {
                var connectResult = await _ledController.ConnectAsync();
                if (!connectResult.Success)
                {
                    return new ServiceResponse
                    {
                        Success = false,
                        Message = $"Failed to connect: {connectResult.Message}",
                        RequestId = request.RequestId
                    };
                }
            }

            // Start the turn-off operation in background and return immediately
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await _ledController.TurnOffAsync();
                    _logger.LogInformation("🔴 Background Turn OFF completed: {Success} - {Message}", result.Success, result.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "💥 Background Turn OFF failed");
                }
            });

            _logger.LogInformation("🔴 Turn OFF command accepted - executing in background");

            return new ServiceResponse
            {
                Success = true,
                Message = "Turn OFF command accepted - executing in background",
                RequestId = request.RequestId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error starting turn OFF command");
            return new ServiceResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                RequestId = request.RequestId
            };
        }
    }

    private async Task<ServiceResponse> HandleGetStatusAsync(ServiceRequest request)
    {
        try
        {
            var patterns = await _ledController!.GetCurrentPatternsAsync();
            
            return new ServiceResponse
            {
                Success = true,
                Message = "Status retrieved",
                RequestId = request.RequestId,
                Data = new Dictionary<string, object>
                {
                    ["connected"] = _ledController.IsConnected,
                    ["patterns"] = patterns ?? new LedStatus()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error getting status");
            return new ServiceResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                RequestId = request.RequestId
            };
        }
    }

    private async Task<ServiceResponse> HandleGetCurrentPatternsAsync(ServiceRequest request)
    {
        try
        {
            var patterns = await _ledController!.GetCurrentPatternsAsync();
            
            return new ServiceResponse
            {
                Success = true,
                Message = "Patterns retrieved",
                RequestId = request.RequestId,
                Data = new Dictionary<string, object>
                {
                    ["patterns"] = patterns ?? new LedStatus()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error getting patterns");
            return new ServiceResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                RequestId = request.RequestId
            };
        }
    }

    private async Task<ServiceResponse> HandleSetColorAsync(ServiceRequest request)
    {
        try
        {
            // Extract color parameters from request
            if (request.Parameters == null || 
                !request.Parameters.TryGetValue("zone", out var zoneObj) ||
                !request.Parameters.TryGetValue("color", out var colorObj))
            {
                return new ServiceResponse
                {
                    Success = false,
                    Message = "Missing zone or color parameters",
                    RequestId = request.RequestId
                };
            }

            // Handle JsonElement values properly
            int zoneValue;
            int colorValue;
            
            if (zoneObj is JsonElement zoneElement)
            {
                zoneValue = zoneElement.GetInt32();
            }
            else
            {
                zoneValue = Convert.ToInt32(zoneObj);
            }
            
            if (colorObj is JsonElement colorElement)
            {
                colorValue = colorElement.GetInt32();
            }
            else
            {
                colorValue = Convert.ToInt32(colorObj);
            }

            var zone = (LedZone)zoneValue;

            var result = await _ledController!.SetZoneColorAsync(zone, colorValue);
            _logger.LogInformation("🎨 Set color result: {Success} - {Message}", result.Success, result.Message);

            return new ServiceResponse
            {
                Success = result.Success,
                Message = result.Message,
                RequestId = request.RequestId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error setting color");
            return new ServiceResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                RequestId = request.RequestId
            };
        }
    }

    private async Task SendResponseAsync(NamedPipeServerStream pipeServer, ServiceResponse response)
    {
        var responseJson = JsonSerializer.Serialize(response);
        var responseBytes = Encoding.UTF8.GetBytes(responseJson);
        
        await pipeServer.WriteAsync(responseBytes);
        await pipeServer.FlushAsync();
        
        _logger.LogDebug("📤 Sent response: {Response}", responseJson);
    }

    public override void Dispose()
    {
        _ledController?.Dispose();
        _operationSemaphore?.Dispose();
        base.Dispose();
    }
}
