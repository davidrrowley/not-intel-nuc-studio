using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using NucLedController.Client;
using NucLedController.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.UI;

namespace NucLedController.WinUI3.Views
{
    public partial class LedsPage : Page
    {
        private NucLedServiceClient? _serviceClient;
        private LedZone _selectedZone = LedZone.Skull;
        private Dictionary<LedZone, ZoneState> _zoneStates;
        private bool _isUpdatingUI = false;

        // Zone state tracking
        private class ZoneState
        {
            public LedPattern Pattern { get; set; } = LedPattern.Static;
            public int Color1 { get; set; } = 16711680; // Red
            public int Color2 { get; set; } = 65280;    // Green  
            public int Color3 { get; set; } = 255;      // Blue
            public int Brightness { get; set; } = 5;
            public int Frequency { get; set; } = 3;
            public bool RainbowEnabled { get; set; } = false;
            public bool Enabled { get; set; } = true;
        }

        public LedsPage()
        {
            try
            {
                WriteDebugToFile("🔧 LedsPage constructor started");
                
                // Initialize state BEFORE InitializeComponent to handle early events
                InitializeZoneStates();
                WriteDebugToFile("🔧 InitializeZoneStates completed");
                
                this.InitializeComponent();
                WriteDebugToFile("🔧 InitializeComponent completed");
                
                // Add loaded event handler (removed duplicate from XAML)
                this.Loaded += OnPageLoaded;
                WriteDebugToFile("🔧 Loaded event handler attached");
                
                // Try calling initialization directly since Loaded event might not fire
                WriteDebugToFile("🔧 Attempting direct initialization");
                try 
                {
                    InitializeColorPickers();
                    WriteDebugToFile("🔧 Direct InitializeColorPickers completed");
                }
                catch (Exception ex)
                {
                    WriteDebugToFile($"💥 Direct InitializeColorPickers failed: {ex.Message}");
                }
                
                WriteDebugToFile("🔧 LedsPage constructor completed successfully");
            }
            catch (Exception ex)
            {
                WriteDebugToFile($"💥 LedsPage constructor error: {ex.Message}");
                WriteDebugToFile($"💥 Stack trace: {ex.StackTrace}");
                throw; // Re-throw to maintain original behavior
            }
        }

        private static void WriteDebugToFile(string message)
        {
            try
            {
                var logFile = System.IO.Path.Combine(@"e:\Users\131858866\Github repos\not_intel_nuc_studio\NucLedController\logs", "NucLedDebug.txt");
                File.AppendAllText(logFile, $"{DateTime.Now:HH:mm:ss.fff} - {message}\n");
            }
            catch { /* ignore file errors */ }
        }

        private void InitializeZoneStates()
        {
            _zoneStates = new Dictionary<LedZone, ZoneState>
            {
                { LedZone.Skull, new ZoneState() },
                { LedZone.FrontBottom, new ZoneState() },
                { LedZone.BottomRight, new ZoneState() },
                { LedZone.BottomLeft, new ZoneState() }
            };
        }

        private void InitializeColorPickers()
        {
            try
            {
                // Add click handlers to standard color palette
                if (StandardColorsPanel?.Children != null)
                {
                    foreach (var child in StandardColorsPanel.Children)
                    {
                        if (child is Border border)
                        {
                            border.PointerPressed += OnStandardColorClicked;
                            border.PointerEntered += OnColorHover;
                            border.PointerExited += OnColorHover;
                        }
                    }
                }

                // Add click handlers to recent color palette
                if (RecentColorsPanel?.Children != null)
                {
                    foreach (var child in RecentColorsPanel.Children)
                    {
                        if (child is Border border)
                        {
                            border.PointerPressed += OnRecentColorClicked;
                            border.PointerEntered += OnColorHover;
                            border.PointerExited += OnColorHover;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing color pickers: {ex.Message}");
            }
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                WriteDebugToFile("🔧 OnPageLoaded started");
                WriteDebugToFile("🔧 About to call InitializeColorPickers");
                InitializeColorPickers();
                WriteDebugToFile("🔧 InitializeColorPickers completed");
                WriteDebugToFile("🔧 About to call InitializeServiceConnectionAsync");
                await InitializeServiceConnectionAsync();
                WriteDebugToFile("🔧 InitializeServiceConnectionAsync completed");
                WriteDebugToFile("🔧 About to call UpdateZoneDisplay");
                UpdateZoneDisplay(_selectedZone);
                WriteDebugToFile("🔧 OnPageLoaded completed successfully");
            }
            catch (Exception ex)
            {
                WriteDebugToFile($"💥 OnPageLoaded error: {ex.Message}");
                WriteDebugToFile($"💥 OnPageLoaded stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"Error loading page: {ex.Message}");
            }
        }

        private async Task InitializeServiceConnectionAsync()
        {
            try
            {
                _serviceClient = new NucLedServiceClient();
                
                // Test connection
                var (pingSuccess, connected, message) = await _serviceClient.PingAsync();
                
                if (pingSuccess && connected)
                {
                    // Get current hardware state
                    var (statusSuccess, status, statusMessage) = await _serviceClient.GetStatusAsync();
                    if (statusSuccess && status != null)
                    {
                        MasterEnableToggle.IsOn = status.ButtonStatus;
                        await SyncWithHardwareState(status);
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle connection error silently for now
                System.Diagnostics.Debug.WriteLine($"Service connection failed: {ex.Message}");
            }
        }

        private async Task SyncWithHardwareState(LedStatus status)
        {
            // Update zone states based on hardware patterns
            foreach (var pattern in status.LedPatternList)
            {
                var zone = HardwareLedPattern.ChannelToZone(pattern.Channel);
                if (_zoneStates.ContainsKey(zone))
                {
                    var zoneState = _zoneStates[zone];
                    zoneState.Color1 = pattern.Color;
                    zoneState.Brightness = pattern.Brightness;
                    zoneState.Enabled = pattern.Enabled;
                    
                    // Map pattern type
                    zoneState.Pattern = pattern.Pattern switch
                    {
                        0 => LedPattern.Off,
                        1 => LedPattern.Static,
                        _ => LedPattern.Static
                    };
                }
            }

            // Update visual indicators
            UpdateAllZoneVisuals();
        }

        private void OnZoneSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Guard against early events during initialization
            if (_zoneStates == null) return;
            
            if (ZoneList.SelectedItem is ListViewItem item && item.Tag is string zoneTag)
            {
                if (Enum.TryParse<LedZone>(zoneTag, true, out var zone))
                {
                    _selectedZone = zone;
                    UpdateZoneDisplay(_selectedZone);
                }
            }
        }

        private void UpdateZoneDisplay(LedZone zone)
        {
            try
            {
                _isUpdatingUI = true;

                var zoneState = _zoneStates[zone];
                
                // Update zone title
                if (ZoneTitle != null)
                    ZoneTitle.Text = GetZoneDisplayName(zone);
                
                // Update pattern selector with safer enum-to-item mapping
                if (PatternSelector != null)
                {
                    foreach (ComboBoxItem item in PatternSelector.Items)
                    {
                        if ((item.Tag?.ToString() ?? "") == zoneState.Pattern.ToString())
                        {
                            PatternSelector.SelectedItem = item;
                            break;
                        }
                    }
                }
                
                // Update controls based on pattern
                UpdateControlsForPattern(zoneState.Pattern);
                
                // Update sliders
                if (BrightnessSlider != null)
                    BrightnessSlider.Value = zoneState.Brightness;
                if (FrequencySlider != null)
                    FrequencySlider.Value = zoneState.Frequency;
                
                // Update toggles
                if (RainbowToggle != null)
                    RainbowToggle.IsOn = zoneState.RainbowEnabled;
                
                // Update color displays
                UpdateColorDisplays(zoneState);

                _isUpdatingUI = false;
            }
            catch (Exception ex)
            {
                _isUpdatingUI = false;
                System.Diagnostics.Debug.WriteLine($"Error updating zone display: {ex.Message}");
            }
        }

        private void UpdateControlsForPattern(LedPattern pattern)
        {
            try
            {
                // Show/hide frequency control for animated patterns
                if (FrequencyPanel != null)
                    FrequencyPanel.Visibility = pattern is LedPattern.PulseTrain1 or LedPattern.PulseTrain2 or LedPattern.PulseTrain3 
                        ? Visibility.Visible : Visibility.Collapsed;

                // Show/hide color controls based on pattern
                if (Color1Panel != null)
                    Color1Panel.Visibility = pattern != LedPattern.Off ? Visibility.Visible : Visibility.Collapsed;
                if (Color2Panel != null)
                    Color2Panel.Visibility = pattern is LedPattern.PulseTrain1 or LedPattern.PulseTrain2 or LedPattern.PulseTrain3 
                        ? Visibility.Visible : Visibility.Collapsed;
                if (Color3Panel != null)
                    Color3Panel.Visibility = pattern is LedPattern.PulseTrain1 or LedPattern.PulseTrain2 or LedPattern.PulseTrain3 
                        ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating controls for pattern: {ex.Message}");
            }
        }

        private void UpdateColorDisplays(ZoneState zoneState)
        {
            try
            {
                if (Color1Display != null)
                    Color1Display.Fill = new SolidColorBrush(IntToColor(zoneState.Color1));
                if (Color2Display != null)
                    Color2Display.Fill = new SolidColorBrush(IntToColor(zoneState.Color2));
                if (Color3Display != null)
                    Color3Display.Fill = new SolidColorBrush(IntToColor(zoneState.Color3));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating color displays: {ex.Message}");
            }
        }

        private void UpdateAllZoneVisuals()
        {
            // Update zone list indicators
            UpdateZoneIndicator(LedZone.Skull, SkullIndicator, SkullStatus);
            UpdateZoneIndicator(LedZone.FrontBottom, BottomFrontIndicator, BottomFrontStatus);
            UpdateZoneIndicator(LedZone.BottomRight, BottomRightIndicator, BottomRightStatus);
            UpdateZoneIndicator(LedZone.BottomLeft, BottomLeftIndicator, BottomLeftStatus);

            // Update visual feedback in NUC displays
            UpdateNucVisualBorder(LedZone.Skull, SkullLedVisual);
            UpdateNucVisualRectangle(LedZone.FrontBottom, FrontBottomLedVisual);
            UpdateNucVisualRectangle(LedZone.BottomLeft, LeftSideLedVisual);
            UpdateNucVisualRectangle(LedZone.BottomRight, RightSideLedVisual);
        }

        private void UpdateZoneIndicator(LedZone zone, Rectangle indicator, TextBlock status)
        {
            var zoneState = _zoneStates[zone];
            indicator.Fill = new SolidColorBrush(IntToColor(zoneState.Color1));
            status.Text = GetPatternDisplayName(zoneState.Pattern);
        }

        private void UpdateNucVisualRectangle(LedZone zone, Rectangle visual)
        {
            var zoneState = _zoneStates[zone];
            if (zoneState.Enabled && zoneState.Pattern != LedPattern.Off)
            {
                visual.Fill = new SolidColorBrush(IntToColor(zoneState.Color1));
                visual.Opacity = Math.Max(0.3, zoneState.Brightness / 5.0);
            }
            else
            {
                visual.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 68, 68, 68));
                visual.Opacity = 0.3;
            }
        }

        private void UpdateNucVisualBorder(LedZone zone, Border visual)
        {
            var zoneState = _zoneStates[zone];
            if (zoneState.Enabled && zoneState.Pattern != LedPattern.Off)
            {
                visual.Background = new SolidColorBrush(IntToColor(zoneState.Color1));
                visual.Opacity = Math.Max(0.3, zoneState.Brightness / 5.0);
            }
            else
            {
                visual.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 68, 68, 68));
                visual.Opacity = 0.3;
            }
        }

        private async void OnPatternChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUI || PatternSelector.SelectedItem is not ComboBoxItem item) return;

            if (Enum.TryParse<LedPattern>(item.Tag?.ToString() ?? "Static", true, out var pattern))
            {
                _zoneStates[_selectedZone].Pattern = pattern;
                UpdateControlsForPattern(pattern);
                await ApplyZoneChanges();
            }
        }

        private async void OnBrightnessChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdatingUI) return;

            _zoneStates[_selectedZone].Brightness = (int)e.NewValue;
            await ApplyZoneChanges();
        }

        private async void OnStandardColorClicked(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Border border && border.Tag is string colorValue)
            {
                var color = int.Parse(colorValue);
                await SetActiveColor(color);
            }
        }

        private async void OnRecentColorClicked(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                var brush = border.Background as SolidColorBrush;
                var color = ColorToInt(brush?.Color ?? Microsoft.UI.Colors.Red);
                await SetActiveColor(color);
            }
        }

        private void OnColorHover(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                // Simple hover effect
                border.Opacity = border.Opacity == 1.0 ? 0.8 : 1.0;
            }
        }

        private async Task SetActiveColor(int color)
        {
            var zoneState = _zoneStates[_selectedZone];
            
            // Set the appropriate color based on pattern
            if (zoneState.Pattern is LedPattern.PulseTrain1 or LedPattern.PulseTrain2 or LedPattern.PulseTrain3)
            {
                // For multi-color patterns, cycle through colors
                // This is simplified - in real implementation, you'd need UI to select which color slot
                zoneState.Color1 = color;
            }
            else
            {
                zoneState.Color1 = color;
            }

            UpdateColorDisplays(zoneState);
            await ApplyZoneChanges();
        }

        private async void OnMasterToggled(object sender, RoutedEventArgs e)
        {
            if (_serviceClient == null) return;

            try
            {
                if (MasterEnableToggle.IsOn)
                {
                    await _serviceClient.TurnOnAsync();
                }
                else
                {
                    await _serviceClient.TurnOffAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Master toggle failed: {ex.Message}");
            }
        }

        private async Task ApplyZoneChanges()
        {
            if (_serviceClient == null) return;

            try
            {
                var zoneState = _zoneStates[_selectedZone];
                
                if (zoneState.Pattern == LedPattern.Off || !zoneState.Enabled)
                {
                    // Turn off the zone
                    await _serviceClient.SetZoneColorAsync(_selectedZone, 0);
                }
                else
                {
                    // Apply color and brightness (Client doesn't support separate brightness parameter)
                    await _serviceClient.SetZoneColorAsync(_selectedZone, zoneState.Color1);
                }

                // Update visual feedback
                UpdateAllZoneVisuals();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Apply zone changes failed: {ex.Message}");
            }
        }

        private void OnBackClicked(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }

        // Helper methods
        private static string GetZoneDisplayName(LedZone zone)
        {
            return zone switch
            {
                LedZone.Skull => "Skull",
                LedZone.FrontBottom => "Bottom Front",
                LedZone.BottomRight => "Bottom Right", 
                LedZone.BottomLeft => "Bottom Left",
                _ => zone.ToString()
            };
        }

        private static string GetPatternDisplayName(LedPattern pattern)
        {
            return pattern switch
            {
                LedPattern.Off => "Off",
                LedPattern.Static => "Solid",
                LedPattern.Breathing => "Breathing",
                LedPattern.Pulse => "Pulse",
                LedPattern.Strobing => "Strobing",
                LedPattern.PulseTrain1 => "PulseTrain1",
                LedPattern.PulseTrain2 => "PulseTrain2", 
                LedPattern.PulseTrain3 => "PulseTrain3",
                LedPattern.Rainbow => "Rainbow1",
                _ => pattern.ToString()
            };
        }

        private static Windows.UI.Color IntToColor(int colorValue)
        {
            return Windows.UI.Color.FromArgb(255,
                (byte)((colorValue >> 16) & 0xFF),
                (byte)((colorValue >> 8) & 0xFF),
                (byte)(colorValue & 0xFF));
        }

        private static int ColorToInt(Windows.UI.Color color)
        {
            return (color.R << 16) | (color.G << 8) | color.B;
        }
    }
}
