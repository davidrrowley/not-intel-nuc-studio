using System;
using System.Collections.Generic;
using System.Management;

namespace NotIntelNucStudio.WinUI3.Services
{
    public class WmiThermalService
    {
        public class ThermalData
        {
            public double CpuTemperature { get; set; }
            public double[] FanSpeeds { get; set; } = Array.Empty<double>();
            public string[] ThermalZones { get; set; } = Array.Empty<string>();
        }

        public ThermalData? GetThermalData()
        {
            var data = new ThermalData();
            bool foundData = false;

            try
            {
                // Try WMI thermal zone query
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_TemperatureProbe");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var currentReading = obj["CurrentReading"];
                    if (currentReading != null)
                    {
                        // Convert from tenths of degrees Kelvin to Celsius
                        data.CpuTemperature = (Convert.ToDouble(currentReading) / 10.0) - 273.15;
                        foundData = true;
                        break;
                    }
                }

                // Try WMI fan speed query
                using var fanSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_Fan");
                var fanSpeeds = new List<double>();
                foreach (ManagementObject obj in fanSearcher.Get())
                {
                    var speed = obj["DesiredSpeed"];
                    if (speed != null)
                    {
                        fanSpeeds.Add(Convert.ToDouble(speed));
                        foundData = true;
                    }
                }
                data.FanSpeeds = fanSpeeds.ToArray();

                return foundData ? data : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
