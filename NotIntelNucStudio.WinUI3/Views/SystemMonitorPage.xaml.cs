using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NotIntelNucStudio.WinUI3.Services;
using System;
using Microsoft.UI.Dispatching;
using System.Management;
using System.Threading.Tasks;
using System.Linq;

namespace NotIntelNucStudio.WinUI3.Views
{
    public sealed partial class SystemMonitorPage : Page
    {
        private DispatcherTimer _updateTimer;
        private Random _random = new Random();

        public SystemMonitorPage()
        {
            Console.WriteLine("=== SystemMonitorPage constructor called ===");
            this.InitializeComponent();
            
            // Load actual system information
            LoadSystemInformation();
            
            // Load EC information
            UpdateEmbeddedControllerInfo();
            
            // Set up update timer for testing UI updates
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromSeconds(2);
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
            Console.WriteLine("=== Timer started - should see updates every 2 seconds ===");
        }

        private async void LoadSystemInformation()
        {
            try
            {
                Console.WriteLine("=== Loading system information ===");
                
                // Get Computer System Information
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    if (FindName("SystemModelText") is TextBlock modelText)
                    {
                        string model = obj["Model"]?.ToString() ?? "Unknown";
                        modelText.Text = $"System Model: {model}";
                        Console.WriteLine($"System Model: {model}");
                    }
                    
                    if (FindName("SystemManufacturerText") is TextBlock manufacturerText)
                    {
                        string manufacturer = obj["Manufacturer"]?.ToString() ?? "Unknown";
                        manufacturerText.Text = $"System Manufacturer: {manufacturer}";
                        Console.WriteLine($"System Manufacturer: {manufacturer}");
                    }
                }

                // Get BIOS Information
                using var biosSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
                foreach (ManagementObject obj in biosSearcher.Get())
                {
                    if (FindName("BiosVersionText") is TextBlock biosText)
                    {
                        string biosVersion = obj["SMBIOSBIOSVersion"]?.ToString() ?? "Unknown";
                        string biosDate = obj["ReleaseDate"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(biosDate) && biosDate.Length >= 8)
                        {
                            // Convert WMI date format (YYYYMMDDHHMMSS) to readable format
                            biosDate = $"{biosDate.Substring(6, 2)}/{biosDate.Substring(4, 2)}/{biosDate.Substring(0, 4)}";
                        }
                        string displayText = string.IsNullOrEmpty(biosDate) ? 
                            $"BIOS Version: {biosVersion}" : 
                            $"BIOS Version: {biosVersion} ({biosDate})";
                        biosText.Text = displayText;
                        Console.WriteLine($"BIOS: {displayText}");
                    }
                }

                // Get BaseBoard Information
                using var boardSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
                foreach (ManagementObject obj in boardSearcher.Get())
                {
                    if (FindName("BaseBoardVersionText") is TextBlock boardText)
                    {
                        string boardVersion = obj["Version"]?.ToString() ?? "Unknown";
                        string boardProduct = obj["Product"]?.ToString() ?? "";
                        string displayText = string.IsNullOrEmpty(boardProduct) ? 
                            $"BaseBoard Version: {boardVersion}" : 
                            $"BaseBoard: {boardProduct} v{boardVersion}";
                        boardText.Text = displayText;
                        Console.WriteLine($"BaseBoard: {displayText}");
                    }
                }

                // Get Computer System Product (for SKU)
                using var productSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystemProduct");
                foreach (ManagementObject obj in productSearcher.Get())
                {
                    if (FindName("SystemSkuText") is TextBlock skuText)
                    {
                        string sku = obj["IdentifyingNumber"]?.ToString() ?? "Unknown";
                        skuText.Text = $"System SKU: {sku}";
                        Console.WriteLine($"System SKU: {sku}");
                    }
                    
                    if (FindName("SystemVersionText") is TextBlock versionText)
                    {
                        string version = obj["Version"]?.ToString() ?? "Unknown";
                        versionText.Text = $"System Version: {version}";
                        Console.WriteLine($"System Version: {version}");
                    }
                }

                Console.WriteLine("=== System information loaded successfully ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR loading system information: {ex.Message}");
                // Fallback to generic values if WMI fails
                if (FindName("SystemModelText") is TextBlock modelText)
                    modelText.Text = "System Model: Detection Failed";
            }
        }

        private void UpdateEmbeddedControllerInfo()
        {
            try
            {
                // Try to get EC version from various sources
                if (FindName("EmbeddedControllerText") is TextBlock ecText)
                {
                    // For Intel NUCs, try to get EC info (this is hardware specific)
                    // For now, we'll show a dynamic version or fallback
                    var ecVersion = GetEmbeddedControllerVersion();
                    ecText.Text = $"Embedded Controller Version: {ecVersion}";
                    Console.WriteLine($"EC Version: {ecVersion}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR getting EC info: {ex.Message}");
            }
        }

        private string GetEmbeddedControllerVersion()
        {
            try
            {
                // Try to get EC version from PnP devices
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%Embedded Controller%'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    string? version = obj["HardwareID"]?.ToString();
                    if (!string.IsNullOrEmpty(version))
                    {
                        return version.Split('\\').LastOrDefault() ?? "Unknown";
                    }
                }
                
                // Fallback: try ACPI
                using var acpiSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_SystemDriver WHERE Name = 'ACPI'");
                foreach (ManagementObject obj in acpiSearcher.Get())
                {
                    return obj["Version"]?.ToString() ?? "Detected";
                }
                
                return "Detected";
            }
            catch
            {
                return "Unknown";
            }
        }

        private void UpdateTimer_Tick(object? sender, object e)
        {
            Console.WriteLine($"=== TIMER TICK at {DateTime.Now:HH:mm:ss} ===");
            
            // Test if we can find and update the UI elements
            if (FindName("CpuUsageText") is TextBlock cpuText)
            {
                var randomCpu = _random.Next(15, 85);
                cpuText.Text = $"{randomCpu}%";
                Console.WriteLine($"Updated CPU to: {randomCpu}%");
            }
            else
            {
                Console.WriteLine("ERROR: Could not find CpuUsageText!");
            }

            if (FindName("CpuTempText") is TextBlock tempText)
            {
                var randomTemp = _random.Next(45, 80);
                tempText.Text = $"{randomTemp}°C";
                Console.WriteLine($"Updated Temp to: {randomTemp}°C");
            }
            else
            {
                Console.WriteLine("ERROR: Could not find CpuTempText!");
            }

            if (FindName("GpuUsageText") is TextBlock gpuText)
            {
                var randomGpu = _random.Next(10, 95);
                gpuText.Text = $"{randomGpu}%";
                Console.WriteLine($"Updated iGPU to: {randomGpu}%");
            }
            else
            {
                Console.WriteLine("ERROR: Could not find GpuUsageText!");
            }

            if (FindName("dGpuUsageText") is TextBlock dGpuText)
            {
                var randomDGpu = _random.Next(5, 75);
                dGpuText.Text = $"{randomDGpu}%";
                Console.WriteLine($"Updated dGPU to: {randomDGpu}%");
            }
            else
            {
                Console.WriteLine("ERROR: Could not find dGpuUsageText!");
            }

            if (FindName("dGpuTempText") is TextBlock dGpuTempText)
            {
                var randomDGpuTemp = _random.Next(35, 70);
                dGpuTempText.Text = $"{randomDGpuTemp}°C";
                Console.WriteLine($"Updated dGPU Temp to: {randomDGpuTemp}°C");
            }
            else
            {
                Console.WriteLine("ERROR: Could not find dGpuTempText!");
            }

            if (FindName("MemoryUsageText") is TextBlock memText)
            {
                var randomMem = _random.Next(40, 80);
                memText.Text = $"{randomMem}%";
                Console.WriteLine($"Updated Memory to: {randomMem}%");
            }
            else
            {
                Console.WriteLine("ERROR: Could not find MemoryUsageText!");
            }

            if (FindName("StorageUsageText") is TextBlock storageText)
            {
                var randomStorage = _random.Next(30, 90);
                storageText.Text = $"{randomStorage}%";
                Console.WriteLine($"Updated Storage to: {randomStorage}%");
            }
            else
            {
                Console.WriteLine("ERROR: Could not find StorageUsageText!");
            }

            // Fan speed updates
            if (FindName("CpuFanRpmText") is TextBlock cpuFanText)
            {
                var randomCpuFan = _random.Next(1500, 2500);
                cpuFanText.Text = $"CPU Fan: {randomCpuFan} RPM";
                Console.WriteLine($"Updated CPU Fan to: {randomCpuFan} RPM");
            }
            else
            {
                Console.WriteLine("ERROR: Could not find CpuFanRpmText!");
            }

            if (FindName("SystemFan1RpmText") is TextBlock systemFan1Text)
            {
                var randomSystemFan1 = _random.Next(800, 1200);
                systemFan1Text.Text = $"System Fan 1: {randomSystemFan1} RPM";
                Console.WriteLine($"Updated System Fan 1 to: {randomSystemFan1} RPM");
            }
            else
            {
                Console.WriteLine("ERROR: Could not find SystemFan1RpmText!");
            }

            if (FindName("SystemFan2RpmText") is TextBlock systemFan2Text)
            {
                var randomSystemFan2 = _random.Next(850, 1250);
                systemFan2Text.Text = $"System Fan 2: {randomSystemFan2} RPM";
                Console.WriteLine($"Updated System Fan 2 to: {randomSystemFan2} RPM");
            }
            else
            {
                Console.WriteLine("ERROR: Could not find SystemFan2RpmText!");
            }

            // Motherboard temperature update
            if (FindName("MotherboardTempText") is TextBlock motherboardTempText)
            {
                var randomMotherboardTemp = _random.Next(28, 45);
                motherboardTempText.Text = $"Motherboard Temp: {randomMotherboardTemp}°C";
                Console.WriteLine($"Updated Motherboard Temp to: {randomMotherboardTemp}°C");
            }
            else
            {
                Console.WriteLine("ERROR: Could not find MotherboardTempText!");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Back button clicked");
            // Stop the timer when navigating back
            _updateTimer?.Stop();
            
            // Navigate back to MainPage
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}
