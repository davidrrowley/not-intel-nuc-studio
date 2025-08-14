using NucLedController.Core.Testing;

namespace NucLedController.CommandExplorer;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Intel NUC LED Command Explorer ===");
        Console.WriteLine("Investigating serial command capabilities...\n");
        
        var explorer = new NucLedCommandExplorer();
        
        Console.WriteLine("🔍 Testing query commands...");
        var queryResults = await explorer.ExploreQueryCommandsAsync("COM3");
        
        foreach (var result in queryResults)
        {
            Console.WriteLine(result);
        }
        
        Console.WriteLine("\n🔊 Testing command echo...");
        var echoResults = await explorer.TestCommandEchoAsync("COM3");
        
        foreach (var result in echoResults)
        {
            Console.WriteLine(result);
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
