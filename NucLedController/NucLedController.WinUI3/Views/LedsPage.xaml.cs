using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NucLedController.Client;
using System;
using System.Threading.Tasks;

namespace NucLedController.WinUI3.Views
{
    public partial class LedsPage : Page
    {
        private NucLedServiceClient? _serviceClient;
        private bool _effectsEnabled = false;

        public LedsPage()
        {
            this.InitializeComponent();
            // Initialize with test mode message
            ConnectionStatus.Text = "Ready to connect to service";
            ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange);
        }

        private NucLedServiceClient GetServiceClient()
        {
            if (_serviceClient == null)
            {
                _serviceClient = new NucLedServiceClient();
            }
            return _serviceClient;
        }

        private void OnBackClicked(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }

        private async void OnEffectsToggled(object sender, RoutedEventArgs e)
        {
            _effectsEnabled = EffectsToggle.IsChecked ?? false;
            EffectsToggle.Content = _effectsEnabled ? "ON" : "OFF";
            EffectsToggle.Background = _effectsEnabled ? 
                new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) : 
                new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 75, 85, 99));
            
            // Control actual hardware through service
            try
            {
                var serviceClient = GetServiceClient();
                if (_effectsEnabled)
                {
                    ConnectionStatus.Text = "Turning LEDs ON...";
                    ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Yellow);
                    var result = await serviceClient.TurnOnAsync();
                    if (result.Success)
                    {
                        ConnectionStatus.Text = "✅ LEDs turned ON";
                        ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGreen);
                    }
                    else
                    {
                        ConnectionStatus.Text = $"❌ Failed to turn ON: {result.Message}";
                        ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange);
                    }
                }
                else
                {
                    ConnectionStatus.Text = "Turning LEDs OFF...";
                    ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Yellow);
                    var result = await serviceClient.TurnOffAsync();
                    if (result.Success)
                    {
                        ConnectionStatus.Text = "✅ LEDs turned OFF";
                        ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGreen);
                    }
                    else
                    {
                        ConnectionStatus.Text = $"❌ Failed to turn OFF: {result.Message}";
                        ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange);
                    }
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus.Text = $"🔥 Service error: {ex.Message}";
                ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
            
            // Update LED visualizations
            UpdateLedVisualizations();
        }

        private void UpdateLedVisualizations()
        {
            if (_effectsEnabled)
            {
                // Show LED zones with their current colors
                SkullLed.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 239, 68, 68)); // Red
                BottomLeftLed.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 16, 185, 129)); // Green
                BottomRightLed.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 59, 130, 246)); // Blue
                FrontBottomLed.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 245, 158, 11)); // Yellow
                SideLed1.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 239, 68, 68)); // Red
                SideLed2.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 16, 185, 129)); // Green
                SideLed3.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 59, 130, 246)); // Blue
            }
            else
            {
                // Show LEDs in default state
                var defaultBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 55, 65, 81));
                SkullLed.Background = defaultBrush;
                BottomLeftLed.Fill = defaultBrush;
                BottomRightLed.Fill = defaultBrush;
                FrontBottomLed.Fill = defaultBrush;
                SideLed1.Fill = defaultBrush;
                SideLed2.Fill = defaultBrush;
                SideLed3.Fill = defaultBrush;
            }
        }

        private async void OnConnectClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                ConnectionStatus.Text = "Connecting to service...";
                ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Yellow);
                
                var serviceClient = GetServiceClient();
                // Test service connection and hardware status
                var (pingSuccess, connected, message) = await serviceClient.PingAsync();
                
                if (pingSuccess && connected)
                {
                    ConnectionStatus.Text = $"✅ Service connected - Hardware: Ready";
                    ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGreen);
                    ConnectButton.Content = "Connected";
                    
                    // Get current hardware status to sync the toggle
                    var (statusSuccess, status, statusMessage) = await serviceClient.GetStatusAsync();
                    if (statusSuccess && status != null)
                    {
                        _effectsEnabled = status.ButtonStatus;
                        EffectsToggle.IsChecked = _effectsEnabled;
                        EffectsToggle.Content = _effectsEnabled ? "ON" : "OFF";
                        EffectsToggle.Background = _effectsEnabled ? 
                            new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) : 
                            new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 75, 85, 99));
                        UpdateLedVisualizations();
                        
                        ConnectionStatus.Text = $"✅ Connected - Hardware LEDs: {(_effectsEnabled ? "ON" : "OFF")}";
                    }
                }
                else
                {
                    ConnectionStatus.Text = $"❌ Service not available: {message}";
                    ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange);
                    ConnectButton.Content = "Service Unavailable";
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus.Text = $"🔥 Service connection error: {ex.Message}";
                ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
        }

        private async void OnSkullRedClicked(object sender, RoutedEventArgs e)
        {
            ConnectionStatus.Text = "Individual colors available through Effects toggle";
            ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightBlue);
            if (_effectsEnabled)
            {
                SkullLed.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 239, 68, 68));
                SideLed1.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 239, 68, 68));
            }
        }

        private async void OnBottomLeftGreenClicked(object sender, RoutedEventArgs e)
        {
            ConnectionStatus.Text = "Individual colors available through Effects toggle";
            ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightBlue);
            if (_effectsEnabled)
            {
                BottomLeftLed.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 16, 185, 129));
                SideLed2.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 16, 185, 129));
            }
        }

        private async void OnBottomRightBlueClicked(object sender, RoutedEventArgs e)
        {
            ConnectionStatus.Text = "Individual colors available through Effects toggle";
            ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightBlue);
            if (_effectsEnabled)
            {
                BottomRightLed.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 59, 130, 246));
                SideLed3.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 59, 130, 246));
            }
        }

        private async void OnFrontBottomYellowClicked(object sender, RoutedEventArgs e)
        {
            ConnectionStatus.Text = "Individual colors available through Effects toggle";
            ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightBlue);
            if (_effectsEnabled)
            {
                FrontBottomLed.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 245, 158, 11));
            }
        }

        private async void OnAllWhiteClicked(object sender, RoutedEventArgs e)
        {
            ConnectionStatus.Text = "Individual colors available through Effects toggle";
            ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightBlue);
            if (_effectsEnabled)
            {
                var whiteBrush = new SolidColorBrush(Microsoft.UI.Colors.White);
                SkullLed.Background = whiteBrush;
                BottomLeftLed.Fill = whiteBrush;
                BottomRightLed.Fill = whiteBrush;
                FrontBottomLed.Fill = whiteBrush;
                SideLed1.Fill = whiteBrush;
                SideLed2.Fill = whiteBrush;
                SideLed3.Fill = whiteBrush;
            }
        }

        private async void OnTurnOffClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var serviceClient = GetServiceClient();
                var result = await serviceClient.TurnOffAsync();
                if (result.Success)
                {
                    ConnectionStatus.Text = "✅ LEDs turned OFF";
                    ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGreen);
                    _effectsEnabled = false;
                    EffectsToggle.IsChecked = false;
                    EffectsToggle.Content = "OFF";
                    EffectsToggle.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 75, 85, 99));
                    UpdateLedVisualizations();
                }
                else
                {
                    ConnectionStatus.Text = $"❌ Turn OFF failed: {result.Message}";
                    ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange);
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus.Text = $"🔥 Service error: {ex.Message}";
                ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
            if (_effectsEnabled)
            {
                var offBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 55, 65, 81));
                SkullLed.Background = offBrush;
                BottomLeftLed.Fill = offBrush;
                BottomRightLed.Fill = offBrush;
                FrontBottomLed.Fill = offBrush;
                SideLed1.Fill = offBrush;
                SideLed2.Fill = offBrush;
                SideLed3.Fill = offBrush;
            }
        }

        private async void OnTurnOnClicked(object sender, RoutedEventArgs e)
        {
            ConnectionStatus.Text = "Individual colors available through Effects toggle";
            ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightBlue);
        }

        private async Task ExecuteLedCommand(Func<Task> command)
        {
            // Service client method - no longer needed
            ConnectionStatus.Text = "Use Effects toggle for LED control";
            ConnectionStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightBlue);
        }
    }
}
