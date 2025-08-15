using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NucLedController.WinUI3.Services
{
    /// <summary>
    /// Service-based NUC LED client using HTTP communication
    /// This replaces the old broken COM port approach with a reliable service-based architecture
    /// </summary>
    public class NucLedClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private bool _disposed = false;

        public NucLedClient(string serviceUrl = "http://localhost:8080")
        {
            _baseUrl = serviceUrl;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        /// <summary>
        /// Test connection to the LED service
        /// </summary>
        public async Task<bool> PingAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/led/ping");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get current LED status from service
        /// </summary>
        public async Task<LedStatus?> GetStatusAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/led/status");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<LedStatus>(json, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetStatusAsync error: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Turn all LEDs on
        /// </summary>
        public async Task<bool> TurnOnAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/led/on", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TurnOnAsync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Turn all LEDs off
        /// </summary>
        public async Task<bool> TurnOffAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/led/off", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TurnOffAsync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Set color for a specific LED zone
        /// </summary>
        public async Task<bool> SetColorAsync(int zone, uint colorArgb)
        {
            try
            {
                var request = new SetColorRequest 
                { 
                    Zone = zone, 
                    Color = colorArgb 
                };
                
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/led/setcolor", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetColorAsync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Set brightness level
        /// </summary>
        public async Task<bool> SetBrightnessAsync(int brightness)
        {
            try
            {
                var request = new SetBrightnessRequest { Brightness = brightness };
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/led/brightness", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetBrightnessAsync error: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// LED status response from service
    /// </summary>
    public class LedStatus
    {
        public bool IsEnabled { get; set; }
        public int Brightness { get; set; }
        public LedZoneStatus[] Zones { get; set; } = Array.Empty<LedZoneStatus>();
    }

    /// <summary>
    /// Individual LED zone status
    /// </summary>
    public class LedZoneStatus
    {
        public int Zone { get; set; }
        public uint Color { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Request to set LED color
    /// </summary>
    public class SetColorRequest
    {
        public int Zone { get; set; }
        public uint Color { get; set; }
    }

    /// <summary>
    /// Request to set LED brightness
    /// </summary>
    public class SetBrightnessRequest
    {
        public int Brightness { get; set; }
    }
}
