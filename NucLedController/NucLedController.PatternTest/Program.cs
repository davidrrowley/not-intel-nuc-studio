using NucLedController.Core;
using NucLedController.Core.Models;

namespace NucLedController.Tests;

/// <summary>
/// Test program to demonstrate Intel SDK-style GetCurrentPatterns() functionality
/// Shows how we've implemented the same API patterns as the real Intel SDK
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🔍 Intel SDK-Style GetCurrentPatterns() Test");
        Console.WriteLine("============================================");
        
        var controller = new Core.NucLedController();
        
        try
        {
            // Initialize with state management
            await controller.InitializeAsync();
            Console.WriteLine("✅ Controller initialized with state management");
            
            // Connect to hardware
            Console.WriteLine("\n🔌 Connecting to COM3...");
            var connectResult = await controller.ConnectAsync("COM3");
            
            if (connectResult.Success)
            {
                Console.WriteLine($"✅ {connectResult.Message}");
                
                // Turn on LEDs for testing
                Console.WriteLine("\n💡 Turning on LEDs...");
                var turnOnResult = await controller.TurnOnAsync();
                if (turnOnResult.Success)
                {
                    Console.WriteLine($"✅ {turnOnResult.Message}");
                    
                    // Test Intel SDK-style GetCurrentPatterns method
                    Console.WriteLine("\n📊 Testing Intel SDK-style GetCurrentPatterns()...");
                    var ledStatus = await controller.GetCurrentPatternsAsync();
                    
                    if (ledStatus != null)
                    {
                        Console.WriteLine($"🔍 Button Status: {ledStatus.ButtonStatus}");
                        Console.WriteLine($"🔍 Pattern Count: {ledStatus.LedPatternList.Length}");
                        Console.WriteLine();
                        
                        foreach (var pattern in ledStatus.LedPatternList)
                        {
                            var zone = HardwareLedPattern.ChannelToZone(pattern.Channel);
                            Console.WriteLine($"Zone {zone}:");
                            Console.WriteLine($"  Channel: {pattern.Channel} ('{(char)pattern.Channel}')");
                            Console.WriteLine($"  Color: {pattern.Color} (0x{pattern.Color:X6})");
                            Console.WriteLine($"  Brightness: {pattern.Brightness}");
                            Console.WriteLine($"  Pattern: {pattern.Pattern}");
                            Console.WriteLine($"  Speed: {pattern.Speed}");
                            Console.WriteLine($"  Enabled: {pattern.Enabled}");
                            Console.WriteLine();
                        }
                    }
                    else
                    {
                        Console.WriteLine("❌ No LED status returned");
                    }
                    
                    // Test enhanced GetCurrentPattern method (Intel SDK style)
                    Console.WriteLine("📊 Testing enhanced GetCurrentPattern() method...");
                    var currentPatternResult = await controller.GetCurrentPatternAsync();
                    if (currentPatternResult.Success)
                    {
                        Console.WriteLine($"✅ {currentPatternResult.Message}");
                    }
                    else
                    {
                        Console.WriteLine($"❌ {currentPatternResult.Message}");
                    }
                    
                    // Test UpdateFromDevice method (Intel SDK style)
                    Console.WriteLine("\n🔄 Testing UpdateFromDevice() method...");
                    var updateResult = await controller.UpdateFromDeviceAsync();
                    if (updateResult.Success)
                    {
                        Console.WriteLine($"✅ {updateResult.Message}");
                        
                        // Show the updated state
                        var currentState = controller.StateManager.CurrentState;
                        Console.WriteLine($"📊 Updated State: {currentState.GetDescription()}");
                    }
                    else
                    {
                        Console.WriteLine($"❌ {updateResult.Message}");
                    }
                    
                    // Set a specific color and test again
                    Console.WriteLine("\n🎨 Setting zone to blue and testing pattern reading...");
                    await controller.SetZoneColorAsync(LedZone.Skull, LedColors.Blue, 4);
                    await Task.Delay(1000);
                    
                    var blueStatus = await controller.GetCurrentPatternsAsync();
                    if (blueStatus != null)
                    {
                        var skullPattern = blueStatus.LedPatternList.FirstOrDefault(p => p.Channel == 65); // 'A'
                        if (skullPattern != null)
                        {
                            Console.WriteLine($"🔍 Skull zone after blue: Color={skullPattern.Color:X6}, Brightness={skullPattern.Brightness}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"❌ Failed to turn on LEDs: {turnOnResult.Message}");
                }
                
                Console.WriteLine("\n🔴 Turning off LEDs...");
                await controller.TurnOffAsync();
            }
            else
            {
                Console.WriteLine($"❌ Connection failed: {connectResult.Message}");
                Console.WriteLine("💡 Make sure your Intel NUC is connected and no other LED applications are running");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 Error: {ex.Message}");
        }
        finally
        {
            await controller.DisconnectAsync();
            controller.Dispose();
        }
        
        Console.WriteLine("\n✅ Test completed. Press any key to exit...");
        Console.ReadKey();
    }
}
