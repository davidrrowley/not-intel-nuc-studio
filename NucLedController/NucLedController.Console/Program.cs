using NucLedController.Core;
using NucLedController.Core.Models;

namespace NucLedController.Console;

class Program
{
    static async Task Main(string[] args)
    {
        System.Console.WriteLine("=== NUC LED Controller Test ===");
        System.Console.WriteLine("Clean class library implementation");
        
        using var controller = new Core.NucLedController();
        
        // Subscribe to events for feedback
        controller.ConnectionChanged += (s, connected) => 
            System.Console.WriteLine($"Connection: {(connected ? "Connected" : "Disconnected")}");
        controller.StatusChanged += (s, status) => 
            System.Console.WriteLine($"Status: {status}");
        
        try
        {
            // Connect
            var connectResult = await controller.ConnectAsync("COM3");
            if (!connectResult.Success)
            {
                System.Console.WriteLine($"Failed to connect: {connectResult.Message}");
                return;
            }
            
            // Simple menu
            while (true)
            {
                System.Console.WriteLine("\n=== LED Control Menu ===");
                System.Console.WriteLine("1. Set Skull to Red");
                System.Console.WriteLine("2. Set Bottom Left to Green");
                System.Console.WriteLine("3. Set Bottom Right to Blue");
                System.Console.WriteLine("4. Set Front Bottom to Yellow");
                System.Console.WriteLine("5. Set All Zones to White");
                System.Console.WriteLine("6. Disable Rainbow Mode");
                System.Console.WriteLine("7. Turn Off LEDs");
                System.Console.WriteLine("8. Turn On LEDs");
                System.Console.WriteLine("Q. Quit");
                System.Console.Write("Choose option: ");
                
                var choice = System.Console.ReadLine()?.ToUpper();
                
                switch (choice)
                {
                    case "1":
                        await controller.SetZoneColorAsync(LedZone.Skull, LedColors.Red);
                        break;
                    case "2":
                        await controller.SetZoneColorAsync(LedZone.BottomLeft, LedColors.Green);
                        break;
                    case "3":
                        await controller.SetZoneColorAsync(LedZone.BottomRight, LedColors.Blue);
                        break;
                    case "4":
                        await controller.SetZoneColorAsync(LedZone.FrontBottom, LedColors.Yellow);
                        break;
                    case "5":
                        await controller.SetAllZonesAsync(LedColors.White);
                        break;
                    case "6":
                        await controller.DisableRainbowAsync();
                        break;
                    case "7":
                        await controller.TurnOffAsync();
                        break;
                    case "8":
                        await controller.TurnOnAsync();
                        break;
                    case "Q":
                        await controller.TurnOffAsync();
                        return;
                    default:
                        System.Console.WriteLine("Invalid choice.");
                        break;
                }
                
                System.Console.WriteLine("Press Enter to continue...");
                System.Console.ReadLine();
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
