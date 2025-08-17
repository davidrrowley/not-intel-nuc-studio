using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Management;
using LibreHardwareMonitor.Hardware;

namespace NotIntelNucStudio.WinUI3.Services
{
    /// <summary>
    /// Self-contained hardware monitoring using LibreHardwareMonitor
    /// This is open source and doesn't require external applications
    /// </summary>
    public class LibreHardwareService : IDisposable
    {
        private Computer? _computer;
        private bool _isInitialized = false;

        public event EventHandler<string>? StatusChanged;

        public class HardwareSensor
        {
            public string Name { get; set; } = string.Empty;
            public string Identifier { get; set; } = string.Empty;
            public float Value { get; set; }
            public string Unit { get; set; } = string.Empty;
            public SensorType Type { get; set; }
            public float Min { get; set; }
            public float Max { get; set; }
            public string HardwareName { get; set; } = string.Empty;
            public HardwareType HardwareType { get; set; }
        }

        /// <summary>
        /// Initialize LibreHardwareMonitor
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                StatusChanged?.Invoke(this, "🔧 Initializing hardware monitoring...");

                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = true,
                    IsMotherboardEnabled = true,
                    IsControllerEnabled = true,
                    IsNetworkEnabled = true,
                    IsStorageEnabled = true
                };

                StatusChanged?.Invoke(this, "🖥️ Opening LibreHardwareMonitor interfaces...");
                _computer.Open();

                // Validate we found hardware
                var hardwareCount = _computer.Hardware.Count;
                StatusChanged?.Invoke(this, $"🔍 Found {hardwareCount} hardware components");

                if (hardwareCount == 0)
                {
                    StatusChanged?.Invoke(this, "⚠️ No hardware detected by LibreHardwareMonitor");
                    return false;
                }

                // Log detected hardware
                foreach (IHardware hardware in _computer.Hardware)
                {
                    StatusChanged?.Invoke(this, $"📋 {hardware.HardwareType}: {hardware.Name}");
                    
                    // Update hardware to get sensor data
                    hardware.Update();
                    
                    // Count sensors for this hardware
                    var sensorCount = hardware.Sensors.Count();
                    StatusChanged?.Invoke(this, $"   📊 {sensorCount} sensors available");
                }

                _isInitialized = true;
                StatusChanged?.Invoke(this, "✅ LibreHardwareMonitor initialized successfully");
                
                return true;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error initializing LibreHardwareMonitor: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get all current sensor readings
        /// </summary>
        public async Task<List<HardwareSensor>> GetAllSensorsAsync()
        {
            var sensors = new List<HardwareSensor>();

            if (!_isInitialized || _computer == null)
            {
                StatusChanged?.Invoke(this, "⚠️ LibreHardwareMonitor not initialized");
                return sensors;
            }

            try
            {
                foreach (IHardware hardware in _computer.Hardware)
                {
                    // Update hardware to get latest sensor readings
                    hardware.Update();

                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.Value.HasValue)
                        {
                            sensors.Add(new HardwareSensor
                            {
                                Name = sensor.Name,
                                Identifier = sensor.Identifier.ToString(),
                                Value = sensor.Value.Value,
                                Unit = GetSensorUnit(sensor.SensorType),
                                Type = sensor.SensorType,
                                Min = sensor.Min ?? 0,
                                Max = sensor.Max ?? 0,
                                HardwareName = hardware.Name,
                                HardwareType = hardware.HardwareType
                            });
                        }
                    }
                }

                StatusChanged?.Invoke(this, $"📊 Retrieved {sensors.Count} sensor readings");
                return sensors;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error reading sensors: {ex.Message}");
                return sensors;
            }
        }

        /// <summary>
        /// Get sensors by type (Temperature, Fan, Voltage, etc.)
        /// </summary>
        public async Task<List<HardwareSensor>> GetSensorsByTypeAsync(SensorType sensorType)
        {
            var allSensors = await GetAllSensorsAsync();
            return allSensors.Where(s => s.Type == sensorType).ToList();
        }

        /// <summary>
        /// Get sensors by hardware type (CPU, GPU, Motherboard, etc.)
        /// </summary>
        public async Task<List<HardwareSensor>> GetSensorsByHardwareAsync(HardwareType hardwareType)
        {
            var allSensors = await GetAllSensorsAsync();
            return allSensors.Where(s => s.HardwareType == hardwareType).ToList();
        }

        /// <summary>
        /// Get critical system metrics for dashboard
        /// </summary>
        public async Task<Dictionary<string, object>> GetSystemMetricsAsync()
        {
            var metrics = new Dictionary<string, object>();

            try
            {
                // Try custom NUC thermal driver first for temperature data
                bool customTempSuccess = false;

                var allSensors = await GetAllSensorsAsync();
                
                // Debug: Log hardware and sensor type overview
                var hardwareTypes = allSensors.GroupBy(s => s.HardwareType).ToList();
                StatusChanged?.Invoke(this, $"🔍 Hardware overview: {string.Join(", ", hardwareTypes.Select(g => $"{g.Key}({g.Count()})"))}");
                
                var sensorTypes = allSensors.GroupBy(s => s.Type).ToList();
                StatusChanged?.Invoke(this, $"🔍 Sensor types: {string.Join(", ", sensorTypes.Select(g => $"{g.Key}({g.Count()})"))}");

                // CPU Temperature (only use LibreHardwareMonitor if custom driver failed)
                if (!customTempSuccess)
                {
                    var cpuTemps = allSensors.Where(s => s.Type == SensorType.Temperature && 
                                                         s.HardwareType == HardwareType.Cpu &&
                                                         s.Name.Contains("Core")).ToList();
                    
                    // Debug: Log what CPU temperature sensors we find
                    var allCpuSensors = allSensors.Where(s => s.HardwareType == HardwareType.Cpu).ToList();
                    StatusChanged?.Invoke(this, $"🌡️ Found {allCpuSensors.Count} CPU sensors total");
                    
                    var allCpuTempSensors = allSensors.Where(s => s.Type == SensorType.Temperature && s.HardwareType == HardwareType.Cpu).ToList();
                    StatusChanged?.Invoke(this, $"🌡️ Found {allCpuTempSensors.Count} CPU temperature sensors: {string.Join(", ", allCpuTempSensors.Select(s => $"{s.Name}={s.Value:F1}°C"))}");
                    
                    if (cpuTemps.Any())
                    {
                        metrics["CpuTemperature"] = Math.Round(cpuTemps.Average(s => s.Value), 1);
                        metrics["CpuTemperatureMax"] = cpuTemps.Max(s => s.Value);
                        StatusChanged?.Invoke(this, $"🌡️ CPU Temperature: {metrics["CpuTemperature"]}°C (from {cpuTemps.Count} core sensors)");
                    }
                    else
                    {
                        StatusChanged?.Invoke(this, "⚠️ No CPU core temperature sensors found!");
                        
                        // Try any CPU temperature sensor as fallback
                        if (allCpuTempSensors.Any())
                        {
                            var fallbackTemp = allCpuTempSensors.First();
                            metrics["CpuTemperature"] = Math.Round(fallbackTemp.Value, 1);
                            StatusChanged?.Invoke(this, $"🌡️ Using fallback CPU temperature: {fallbackTemp.Name} = {fallbackTemp.Value:F1}°C");
                        }
                    }
                }

                // CPU Usage
                var cpuUsage = allSensors.FirstOrDefault(s => s.Type == SensorType.Load && 
                                                             s.HardwareType == HardwareType.Cpu &&
                                                             s.Name.Contains("Total"));
                if (cpuUsage != null)
                {
                    metrics["CpuUsage"] = Math.Round(cpuUsage.Value, 1);
                }

                // GPU Usage
                var gpuUsage = allSensors.FirstOrDefault(s => s.Type == SensorType.Load && 
                                                             s.HardwareType == HardwareType.GpuNvidia);
                if (gpuUsage != null)
                {
                    metrics["GpuUsage"] = Math.Round(gpuUsage.Value, 1);
                }

                // GPU Temperature
                var gpuTemp = allSensors.FirstOrDefault(s => s.Type == SensorType.Temperature && 
                                                            s.HardwareType == HardwareType.GpuNvidia);
                if (gpuTemp != null)
                {
                    metrics["GpuTemperature"] = Math.Round(gpuTemp.Value, 1);
                }

                // CPU Clock Speed
                var cpuClock = allSensors.FirstOrDefault(s => s.Type == SensorType.Clock && 
                                                             s.HardwareType == HardwareType.Cpu);
                if (cpuClock != null)
                {
                    metrics["CpuClock"] = Math.Round(cpuClock.Value, 0);
                }

                // Memory Usage
                var memUsage = allSensors.FirstOrDefault(s => s.Type == SensorType.Load && 
                                                             s.HardwareType == HardwareType.Memory);
                if (memUsage != null)
                {
                    metrics["MemoryUsage"] = Math.Round(memUsage.Value, 1);
                }

                // System/Motherboard Temperature
                var systemTemp = allSensors.FirstOrDefault(s => s.Type == SensorType.Temperature && 
                                                               s.HardwareType == HardwareType.Motherboard);
                if (systemTemp != null)
                {
                    metrics["SystemTemperature"] = Math.Round(systemTemp.Value, 1);
                }

                // Overall System Health (average of CPU, GPU, Memory usage)
                var systemHealthComponents = new List<double>();
                if (metrics.ContainsKey("CpuUsage"))
                    systemHealthComponents.Add(Convert.ToDouble(metrics["CpuUsage"]));
                if (metrics.ContainsKey("GpuUsage"))
                    systemHealthComponents.Add(Convert.ToDouble(metrics["GpuUsage"]));
                if (metrics.ContainsKey("MemoryUsage"))
                    systemHealthComponents.Add(Convert.ToDouble(metrics["MemoryUsage"]));
                
                if (systemHealthComponents.Any())
                {
                    metrics["SystemHealth"] = Math.Round(systemHealthComponents.Average(), 1);
                }

                // Fan Speeds
                var fans = allSensors.Where(s => s.Type == SensorType.Fan).ToList();
                if (fans.Any())
                {
                    metrics["FanCount"] = fans.Count;
                    metrics["FanSpeedAvg"] = Math.Round(fans.Average(s => s.Value), 0);
                    metrics["FanSpeedMax"] = fans.Max(s => s.Value);
                    
                    // Debug: Log all fan sensors found
                    StatusChanged?.Invoke(this, $"🌀 Found {fans.Count} fan sensors: {string.Join(", ", fans.Select(f => $"{f.Name}={f.Value:F0}RPM"))}");
                    
                    // Individual fan speeds for specific identification
                    var cpuFan = fans.FirstOrDefault(f => f.Name.ToLower().Contains("cpu"));
                    if (cpuFan != null)
                    {
                        metrics["CpuFanSpeed"] = Math.Round(cpuFan.Value, 0);
                        StatusChanged?.Invoke(this, $"🌀 CPU Fan detected: {cpuFan.Name} = {cpuFan.Value:F0} RPM");
                    }
                    else
                    {
                        // Try first fan as CPU fan if no specific CPU fan found
                        if (fans.Count > 0)
                        {
                            metrics["CpuFanSpeed"] = Math.Round(fans[0].Value, 0);
                            StatusChanged?.Invoke(this, $"🌀 Using first fan as CPU: {fans[0].Name} = {fans[0].Value:F0} RPM");
                        }
                    }
                    
                    var systemFans = fans.Where(f => !f.Name.ToLower().Contains("cpu")).ToList();
                    for (int i = 0; i < systemFans.Count && i < 3; i++)
                    {
                        metrics[$"SystemFan{i + 1}Speed"] = Math.Round(systemFans[i].Value, 0);
                        metrics[$"SystemFan{i + 1}Name"] = systemFans[i].Name;
                        StatusChanged?.Invoke(this, $"🌀 System Fan {i + 1}: {systemFans[i].Name} = {systemFans[i].Value:F0} RPM");
                    }
                }

                // Power Consumption
                var power = allSensors.FirstOrDefault(s => s.Type == SensorType.Power && 
                                                          s.HardwareType == HardwareType.Cpu);
                if (power != null)
                {
                    metrics["PowerConsumption"] = Math.Round(power.Value, 1);
                }

                StatusChanged?.Invoke(this, $"📊 System metrics: CPU {metrics.GetValueOrDefault("CpuTemperature", "N/A")}°C, Usage {metrics.GetValueOrDefault("CpuUsage", "N/A")}%");

                return metrics;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error getting system metrics: {ex.Message}");
                return metrics;
            }
        }

        /// <summary>
        /// Get sensor unit string based on sensor type
        /// </summary>
        private static string GetSensorUnit(SensorType sensorType)
        {
            return sensorType switch
            {
                SensorType.Temperature => "°C",
                SensorType.Fan => "RPM",
                SensorType.Voltage => "V",
                SensorType.Clock => "MHz",
                SensorType.Load => "%",
                SensorType.Power => "W",
                SensorType.Data => "GB",
                SensorType.SmallData => "MB",
                SensorType.Flow => "L/h",
                SensorType.Control => "%",
                SensorType.Level => "%",
                SensorType.Factor => "x",
                SensorType.Frequency => "Hz",
                SensorType.Throughput => "B/s",
                _ => ""
            };
        }

        /// <summary>
        /// Get system information from WMI and hardware sensors
        /// </summary>
        public async Task<Dictionary<string, string>> GetSystemInfoAsync()
        {
            var systemInfo = new Dictionary<string, string>();

            try
            {
                // Get system information from WMI
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        systemInfo["SystemModel"] = obj["Model"]?.ToString() ?? "Unknown";
                        systemInfo["SystemManufacturer"] = obj["Manufacturer"]?.ToString() ?? "Unknown";
                        break;
                    }
                }

                // Get system product information
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystemProduct"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        systemInfo["SystemSku"] = obj["IdentifyingNumber"]?.ToString() ?? "Unknown";
                        systemInfo["SystemVersion"] = obj["Version"]?.ToString() ?? "Unknown";
                        break;
                    }
                }

                // Get BIOS information
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_BIOS"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        systemInfo["BiosVersion"] = obj["SMBIOSBIOSVersion"]?.ToString() ?? "Unknown";
                        systemInfo["EmbeddedController"] = obj["EmbeddedControllerMajorVersion"]?.ToString() + "." + obj["EmbeddedControllerMinorVersion"]?.ToString() ?? "Unknown";
                        break;
                    }
                }

                // Get CPU information
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        systemInfo["CpuName"] = obj["Name"]?.ToString()?.Trim() ?? "Unknown";
                        break;
                    }
                }

                // Get motherboard information
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        systemInfo["BaseBoardVersion"] = obj["Version"]?.ToString() ?? "Unknown";
                        systemInfo["BaseBoardProduct"] = obj["Product"]?.ToString() ?? "Unknown";
                        break;
                    }
                }

                StatusChanged?.Invoke(this, "📋 System information retrieved successfully");
                return systemInfo;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error getting system info: {ex.Message}");
                return systemInfo;
            }
        }

        public void Dispose()
        {
            try
            {
                StatusChanged?.Invoke(this, "🔧 Shutting down hardware monitoring...");
                
                _computer?.Close();
                _computer = null;
                _isInitialized = false;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Error disposing hardware monitoring: {ex.Message}");
            }
        }
    }
}
