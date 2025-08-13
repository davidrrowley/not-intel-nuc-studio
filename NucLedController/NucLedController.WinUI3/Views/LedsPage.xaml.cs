using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NucLedController.Core;
using NucLedController.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace NucLedController.WinUI3.Views
{
    public partial class LedsPage : Page
    {
        private INucLedController? _ledController;
        private bool _effectsEnabled = false;

        public LedsPage()
        {
            this.InitializeComponent();
        }

        private void OnBackClicked(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }

        private void OnEffectsToggled(object sender, RoutedEventArgs e)
        {
            _effectsEnabled = EffectsToggle.IsChecked ?? false;
            EffectsToggle.Content = _effectsEnabled ? "ON" : "OFF";
            EffectsToggle.Background = _effectsEnabled ? 
                new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) : 
                new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 75, 85, 99));
            
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
                _ledController = new Core.NucLedController();
                var result = await _ledController.ConnectAsync();
                
                if (result.Success)
                {
                    ConnectionStatus.Text = "Connected";
                    ConnectionStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGreen);
                    ConnectButton.Content = "Disconnect";
                }
                else
                {
                    ConnectionStatus.Text = $"Connection failed: {result.Message}";
                    ConnectionStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus.Text = $"Error: {ex.Message}";
                ConnectionStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
            }
        }

        private async void OnSkullRedClicked(object sender, RoutedEventArgs e)
        {
            await ExecuteLedCommand(() => _ledController?.SetZoneColorAsync(Core.Models.LedZone.Skull, 0xFF0000));
            if (_effectsEnabled)
            {
                SkullLed.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 239, 68, 68));
                SideLed1.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 239, 68, 68));
            }
        }

        private async void OnBottomLeftGreenClicked(object sender, RoutedEventArgs e)
        {
            await ExecuteLedCommand(() => _ledController?.SetZoneColorAsync(Core.Models.LedZone.BottomLeft, 0x00FF00));
            if (_effectsEnabled)
            {
                BottomLeftLed.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 16, 185, 129));
                SideLed2.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 16, 185, 129));
            }
        }

        private async void OnBottomRightBlueClicked(object sender, RoutedEventArgs e)
        {
            await ExecuteLedCommand(() => _ledController?.SetZoneColorAsync(Core.Models.LedZone.BottomRight, 0x0000FF));
            if (_effectsEnabled)
            {
                BottomRightLed.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 59, 130, 246));
                SideLed3.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 59, 130, 246));
            }
        }

        private async void OnFrontBottomYellowClicked(object sender, RoutedEventArgs e)
        {
            await ExecuteLedCommand(() => _ledController?.SetZoneColorAsync(Core.Models.LedZone.FrontBottom, 0xFFFF00));
            if (_effectsEnabled)
            {
                FrontBottomLed.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 245, 158, 11));
            }
        }

        private async void OnAllWhiteClicked(object sender, RoutedEventArgs e)
        {
            await ExecuteLedCommand(() => _ledController?.SetAllZonesAsync(0xFFFFFF));
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
            await ExecuteLedCommand(() => _ledController?.TurnOffAsync());
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
            await ExecuteLedCommand(() => _ledController?.TurnOnAsync());
        }

        private async Task ExecuteLedCommand(Func<Task<Core.Models.LedCommandResult>?> command)
        {
            if (_ledController == null)
            {
                ConnectionStatus.Text = "Not connected to NUC";
                ConnectionStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                return;
            }

            try
            {
                var result = await command();
                if (result?.Success == true)
                {
                    ConnectionStatus.Text = "Command executed successfully";
                    ConnectionStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGreen);
                }
                else
                {
                    ConnectionStatus.Text = $"Command failed: {result?.Message}";
                    ConnectionStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus.Text = $"Error: {ex.Message}";
                ConnectionStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
            }
        }
    }
}
