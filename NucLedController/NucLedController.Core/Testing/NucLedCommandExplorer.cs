using System.IO.Ports;
using System.Text;
using NucLedController.Core.Models;
using NucLedController.Core.Interfaces;

namespace NucLedController.Core.Testing;

/// <summary>
/// Test class to discover Intel NUC LED serial command capabilities
/// Investigates what query/status commands are available
/// </summary>
public class NucLedCommandExplorer
{
    private SerialPort? _serialPort;
    
    /// <summary>
    /// Test common query commands to see if we can read hardware state
    /// </summary>
    public async Task<List<string>> ExploreQueryCommandsAsync(string portName = "COM3")
    {
        var results = new List<string>();
        
        try
        {
            _serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
            _serialPort.Open();
            await Task.Delay(2000); // Allow connection
            
            // Common query commands to test
            var queryCommands = new[]
            {
                "?",           // Generic help/status
                "STATUS",      // Status query
                "GET",         // Get current state
                "QUERY",       // Query command
                "INFO",        // Information
                "STATE",       // Current state
                "HELP",        // Help command
                "VER",         // Version
                "VERSION",     // Version info
                "LIST",        // List settings
                "SHOW",        // Show current
                "READ",        // Read state
                "CHECK",       // Check status
                "CURRENT",     // Current settings
                "AV?",         // Zone A brightness query
                "BV?",         // Zone B brightness query  
                "CV?",         // Zone C brightness query
                "DV?",         // Zone D brightness query
                "C1?",         // Color 1 query
                "C2?",         // Color 2 query
                "C3?",         // Color 3 query
                "C4?",         // Color 4 query
                "BTN?",        // Button state query
                "AP?",         // Zone A pattern query
                "BP?",         // Zone B pattern query
                "CP?",         // Zone C pattern query
                "DP?",         // Zone D pattern query
                "AR?",         // Zone A rainbow query
                "BR?",         // Zone B rainbow query
                "CR?",         // Zone C rainbow query
                "DR?"          // Zone D rainbow query
            };
            
            foreach (var command in queryCommands)
            {
                try
                {
                    results.Add($"Testing: {command}");
                    
                    // Send command
                    var cmd = command + "\n";
                    var bytes = Encoding.UTF8.GetBytes(cmd);
                    _serialPort.Write(bytes, 0, bytes.Length);
                    
                    // Wait for response
                    await Task.Delay(500);
                    
                    // Try to read response
                    if (_serialPort.BytesToRead > 0)
                    {
                        var buffer = new byte[_serialPort.BytesToRead];
                        _serialPort.Read(buffer, 0, buffer.Length);
                        var response = Encoding.UTF8.GetString(buffer).Trim();
                        
                        if (!string.IsNullOrWhiteSpace(response))
                        {
                            results.Add($"✅ {command} -> {response}");
                        }
                        else
                        {
                            results.Add($"⚪ {command} -> (empty response)");
                        }
                    }
                    else
                    {
                        results.Add($"❌ {command} -> (no response)");
                    }
                    
                    // Clear any remaining data
                    if (_serialPort.BytesToRead > 0)
                    {
                        _serialPort.DiscardInBuffer();
                    }
                }
                catch (Exception ex)
                {
                    results.Add($"💥 {command} -> Error: {ex.Message}");
                }
                
                await Task.Delay(200); // Prevent overwhelming device
            }
        }
        catch (Exception ex)
        {
            results.Add($"Connection error: {ex.Message}");
        }
        finally
        {
            _serialPort?.Close();
            _serialPort?.Dispose();
        }
        
        return results;
    }
    
    /// <summary>
    /// Test if device echoes commands or provides any feedback
    /// </summary>
    public async Task<List<string>> TestCommandEchoAsync(string portName = "COM3")
    {
        var results = new List<string>();
        
        try
        {
            _serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
            _serialPort.Open();
            await Task.Delay(2000);
            
            // Test if known commands give responses
            var testCommands = new[] { "RST", "BTN:1", "C1:16711680", "BTN:0" };
            
            foreach (var command in testCommands)
            {
                try
                {
                    results.Add($"Testing echo for: {command}");
                    
                    // Clear buffer first
                    _serialPort.DiscardInBuffer();
                    
                    // Send command
                    var cmd = command + "\n";
                    var bytes = Encoding.UTF8.GetBytes(cmd);
                    _serialPort.Write(bytes, 0, bytes.Length);
                    
                    // Wait and check for any response
                    await Task.Delay(1000);
                    
                    if (_serialPort.BytesToRead > 0)
                    {
                        var buffer = new byte[_serialPort.BytesToRead];
                        _serialPort.Read(buffer, 0, buffer.Length);
                        var response = Encoding.UTF8.GetString(buffer).Trim();
                        results.Add($"📡 {command} echoed: '{response}'");
                    }
                    else
                    {
                        results.Add($"🔇 {command} -> no echo");
                    }
                }
                catch (Exception ex)
                {
                    results.Add($"💥 Echo test {command}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            results.Add($"Echo test connection error: {ex.Message}");
        }
        finally
        {
            _serialPort?.Close();
            _serialPort?.Dispose();
        }
        
        return results;
    }
}
