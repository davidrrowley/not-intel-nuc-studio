using NucLedController.Client;

Console.WriteLine("🧪 NUC LED Service Test Client");
Console.WriteLine("===============================");

using var client = new NucLedServiceClient();

try
{
    // Test 1: Ping the service
    Console.WriteLine("📡 Testing service connection...");
    var (pingSuccess, connected, pingMessage) = await client.PingAsync();
    
    if (pingSuccess)
    {
        Console.WriteLine($"✅ Service is running! Hardware connected: {connected}");
        Console.WriteLine($"   Message: {pingMessage}");
        Console.WriteLine("\nPress Enter to continue to status test...");
        Console.ReadLine();
    }
    else
    {
        Console.WriteLine($"❌ Service not available: {pingMessage}");
        return;
    }

    // Test 2: Get current status
    Console.WriteLine("\n🔍 Getting current status...");
    var (statusSuccess, status, statusMessage) = await client.GetStatusAsync();
    
    if (statusSuccess)
    {
        Console.WriteLine($"✅ Status retrieved: {statusMessage}");
        if (status != null)
        {
            Console.WriteLine($"   Button Status: {status.ButtonStatus}");
            Console.WriteLine($"   LED Patterns: {status.LedPatternList.Length} patterns");
        }
        Console.WriteLine("\nPress Enter to continue to LED ON test...");
        Console.ReadLine();
    }
    else
    {
        Console.WriteLine($"❌ Failed to get status: {statusMessage}");
    }

    // Test 3: Turn LEDs ON
    Console.WriteLine("\n🟢 Testing LED ON...");
    var turnOnResult = await client.TurnOnAsync();
    
    if (turnOnResult.Success)
    {
        Console.WriteLine($"✅ LEDs turned ON: {turnOnResult.Message}");
        Console.WriteLine("💡 Check your hardware - LEDs should be turning ON now!");
        Console.WriteLine("Press Enter after LEDs are fully on to check button status...");
        Console.ReadLine();
        
        // Small delay to let hardware update its state
        Console.WriteLine("⏱️ Waiting a moment for hardware to update...");
        await Task.Delay(2000); // Increased to 2 seconds
        
        // Check status after turning on and user confirms
        Console.WriteLine("📊 Button status AFTER turning on:");
        var (postOnStatusSuccess, postOnStatus, postOnMessage) = await client.GetStatusAsync();
        if (postOnStatusSuccess && postOnStatus != null)
        {
            Console.WriteLine($"   Button Status: {postOnStatus.ButtonStatus}");
        }
        
        Console.WriteLine("Press Enter to continue to OFF test...");
        Console.ReadLine();
    }
    else
    {
        Console.WriteLine($"❌ Failed to turn LEDs ON: {turnOnResult.Message}");
    }

    // Remove the automatic wait
    // Console.WriteLine("\n⏱️ Waiting 3 seconds...");
    // await Task.Delay(3000);

    // Test 4: Turn LEDs OFF  
    Console.WriteLine("\n🔴 Testing LED OFF...");
    var turnOffResult = await client.TurnOffAsync();
    
    if (turnOffResult.Success)
    {
        Console.WriteLine($"✅ LEDs turned OFF: {turnOffResult.Message}");
        Console.WriteLine("🔹 Check your hardware - LEDs should be turning OFF now!");
        Console.WriteLine("Press Enter after LEDs are fully off to check button status...");
        Console.ReadLine();
        
        // Small delay to let hardware update its state
        Console.WriteLine("⏱️ Waiting a moment for hardware to update...");
        await Task.Delay(2000); // Increased to 2 seconds
        
        // Check status after turning off and user confirms
        Console.WriteLine("📊 Button status AFTER turning off:");
        var (postOffStatusSuccess, postOffStatus, postOffMessage) = await client.GetStatusAsync();
        if (postOffStatusSuccess && postOffStatus != null)
        {
            Console.WriteLine($"   Button Status: {postOffStatus.ButtonStatus}");
        }
        
        Console.WriteLine("Press Enter to complete test...");
        Console.ReadLine();
    }
    else
    {
        Console.WriteLine($"❌ Failed to turn LEDs OFF: {turnOffResult.Message}");
    }

    Console.WriteLine("\n🎉 Service test completed!");
}
catch (Exception ex)
{
    Console.WriteLine($"💥 Test failed with exception: {ex.Message}");
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();
