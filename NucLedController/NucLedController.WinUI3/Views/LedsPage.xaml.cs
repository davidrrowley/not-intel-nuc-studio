#nullable enable

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using NucLedController.WinUI3.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.UI;

namespace NucLedController.WinUI3.Views
{
    public partial class LedsPage : Page
    {
        private string _selectedZone = "Skull";
        private Dictionary<string, ZoneState>? _zoneStates;
        private bool _isUpdatingUI = false;

        // Zone state tracking
        private class ZoneState
        {
            public string Pattern { get; set; } = "Solid";
            public string Color { get; set; } = "Red";
            public int Brightness { get; set; } = 100;
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
                var debugFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "logs", "NucLedDebug.txt");
                var logsDirectory = System.IO.Path.GetDirectoryName(debugFile);
                if (logsDirectory != null && !Directory.Exists(logsDirectory))
                {
                    Directory.CreateDirectory(logsDirectory);
                }
                File.AppendAllText(debugFile, $"{DateTime.Now:HH:mm:ss.fff} - {message}\n");
            }
            catch { /* ignore file errors */ }
        }

        private void InitializeZoneStates()
        {
            _zoneStates = new Dictionary<string, ZoneState>
            {
                { "Skull", new ZoneState() },
                { "BottomFront", new ZoneState() },
                { "BottomRight", new ZoneState() },
                { "BottomLeft", new ZoneState() }
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
                WriteDebugToFile("🔌 Initializing service connection via singleton...");
                
                // Test connection using singleton
                var connectResult = await LedServiceManager.Instance.EnsureConnectedAsync();
                WriteDebugToFile($"🔌 Connection result: Success={connectResult.Success}, Message={connectResult.Message}");
                
                if (connectResult.Success)
                {
                    // Get current hardware state
                    var statusResult = await LedServiceManager.Instance.GetStatusAsync();
                    WriteDebugToFile($"🔌 Status result: Success={statusResult.Success}, Message={statusResult.Message}");
                    
                    if (statusResult.Success)
                    {
                        MasterEnableToggle.IsOn = true; // Default to on since we're connected
                        WriteDebugToFile("🔌 Service connected successfully via singleton");
                    }
                }
            }
            catch (Exception ex)
            {
                WriteDebugToFile($"💥 Service connection failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Service connection failed: {ex.Message}");
            }
        }

        private void OnZoneSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Guard against early events during initialization
            if (_zoneStates == null) return;
            
            if (ZoneList.SelectedItem is ListViewItem item && item.Tag is string zoneTag)
            {
                _selectedZone = zoneTag;
                WriteDebugToFile($"🎯 Zone selected: {_selectedZone}");
                UpdateZoneDisplay(_selectedZone);
            }
        }

        private void UpdateZoneDisplay(string zone)
        {
            try
            {
                _isUpdatingUI = true;

                var zoneState = _zoneStates[zone];
                
                // Update zone title
                if (ZoneTitle != null)
                    ZoneTitle.Text = GetZoneDisplayName(zone);
                
                // Update pattern selector with safer string-to-item mapping
                if (PatternSelector != null)
                {
                    foreach (ComboBoxItem item in PatternSelector.Items)
                    {
                        if ((item.Tag?.ToString() ?? "") == zoneState.Pattern)
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
                    BrightnessSlider.Value = zoneState.Brightness / 20; // Convert 0-100 to 0-5
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
                WriteDebugToFile($"💥 Error updating zone display: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error updating zone display: {ex.Message}");
            }
        }

        private void UpdateControlsForPattern(string pattern)
        {
            try
            {
                // Show/hide frequency control for animated patterns
                if (FrequencyPanel != null)
                    FrequencyPanel.Visibility = pattern is "PulseTrain1" or "PulseTrain2" or "PulseTrain3" 
                        ? Visibility.Visible : Visibility.Collapsed;

                // Show/hide color controls based on pattern
                if (Color1Panel != null)
                    Color1Panel.Visibility = pattern != "Off" ? Visibility.Visible : Visibility.Collapsed;
                if (Color2Panel != null)
                    Color2Panel.Visibility = pattern is "PulseTrain1" or "PulseTrain2" or "PulseTrain3" 
                        ? Visibility.Visible : Visibility.Collapsed;
                if (Color3Panel != null)
                    Color3Panel.Visibility = pattern is "PulseTrain1" or "PulseTrain2" or "PulseTrain3" 
                        ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                WriteDebugToFile($"💥 Error updating controls for pattern: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error updating controls for pattern: {ex.Message}");
            }
        }

        private void UpdateColorDisplays(ZoneState zoneState)
        {
            try
            {
                if (Color1Display != null)
                    Color1Display.Fill = new SolidColorBrush(StringToColor(zoneState.Color));
                // Color2 and Color3 displays would be updated if we had multiple colors for patterns
            }
            catch (Exception ex)
            {
                WriteDebugToFile($"💥 Error updating color displays: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error updating color displays: {ex.Message}");
            }
        }

        private void UpdateAllZoneVisuals()
        {
            // Update zone list indicators
            UpdateZoneIndicator("Skull", SkullIndicator, SkullStatus);
            UpdateZoneIndicator("BottomFront", BottomFrontIndicator, BottomFrontStatus);
            UpdateZoneIndicator("BottomRight", BottomRightIndicator, BottomRightStatus);
            UpdateZoneIndicator("BottomLeft", BottomLeftIndicator, BottomLeftStatus);

            // Update visual feedback in NUC displays
            UpdateNucVisualBorder("Skull", SkullLedVisual);
            UpdateNucVisualRectangle("BottomFront", FrontBottomLedVisual);
            UpdateNucVisualRectangle("BottomLeft", LeftSideLedVisual);
            UpdateNucVisualRectangle("BottomRight", RightSideLedVisual);
        }

        private void UpdateZoneIndicator(string zone, Rectangle? indicator, TextBlock? status)
        {
            var zoneState = _zoneStates[zone];
            if (indicator != null)
                indicator.Fill = new SolidColorBrush(StringToColor(zoneState.Color));
            if (status != null)
                status.Text = GetPatternDisplayName(zoneState.Pattern);
        }

        private void UpdateNucVisualRectangle(string zone, Rectangle visual)
        {
            var zoneState = _zoneStates[zone];
            if (zoneState.Enabled && zoneState.Pattern != "Off")
            {
                visual.Fill = new SolidColorBrush(StringToColor(zoneState.Color));
                visual.Opacity = Math.Max(0.3, zoneState.Brightness / 100.0);
            }
            else
            {
                visual.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 68, 68, 68));
                visual.Opacity = 0.3;
            }
        }

        private void UpdateNucVisualBorder(string zone, Border visual)
        {
            var zoneState = _zoneStates[zone];
            if (zoneState.Enabled && zoneState.Pattern != "Off")
            {
                visual.Background = new SolidColorBrush(StringToColor(zoneState.Color));
                visual.Opacity = Math.Max(0.3, zoneState.Brightness / 100.0);
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

            var pattern = item.Tag?.ToString() ?? "Solid";
            WriteDebugToFile($"🎨 Pattern changed to: {pattern} for zone: {_selectedZone}");
            
            _zoneStates[_selectedZone].Pattern = pattern;
            UpdateControlsForPattern(pattern);
            await ApplyZoneChanges();
        }

        private async void OnBrightnessChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdatingUI) return;

            var brightness = (int)(e.NewValue * 20); // Convert 0-5 to 0-100
            WriteDebugToFile($"🔆 Brightness changed to: {brightness} for zone: {_selectedZone}");
            
            _zoneStates[_selectedZone].Brightness = brightness;
            await ApplyZoneChanges();
        }

        private async void OnStandardColorClicked(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            WriteDebugToFile($"🎨 OnStandardColorClicked - sender type: {sender?.GetType().Name}");
            
            if (sender is Border border)
            {
                var color = GetColorNameFromBrush(border.Background as SolidColorBrush);
                WriteDebugToFile($"🎨 Color name from brush: '{color}', selected zone: {_selectedZone}");
                await SetActiveColor(color);
            }
            else
            {
                WriteDebugToFile($"🎨 Failed to get color - not a border: {sender?.GetType().Name}");
            }
        }

        private async void OnRecentColorClicked(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                var color = GetColorNameFromBrush(border.Background as SolidColorBrush);
                WriteDebugToFile($"🎨 Recent color clicked: {color}");
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

        private async Task SetActiveColor(string color)
        {
            WriteDebugToFile($"🎨 SetActiveColor called with color: {color}, zone: {_selectedZone}");
            
            var zoneState = _zoneStates[_selectedZone];
            zoneState.Color = color;

            WriteDebugToFile($"🎨 Zone state updated - Color: {zoneState.Color}, Pattern: {zoneState.Pattern}");
            UpdateColorDisplays(zoneState);
            await ApplyZoneChanges();
        }

        private async void OnMasterToggled(object sender, RoutedEventArgs e)
        {
            try
            {
                WriteDebugToFile($"🔌 Master toggle changed: {MasterEnableToggle.IsOn}");
                
                if (MasterEnableToggle.IsOn)
                {
                    var result = await LedServiceManager.Instance.SetAllZonesAsync("Solid", "Blue", 100);
                    WriteDebugToFile($"🔌 Turn on result: Success={result.Success}, Message={result.Message}");
                }
                else
                {
                    var result = await LedServiceManager.Instance.TurnOffAllAsync();
                    WriteDebugToFile($"🔌 Turn off result: Success={result.Success}, Message={result.Message}");
                }
            }
            catch (Exception ex)
            {
                WriteDebugToFile($"💥 Master toggle failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Master toggle failed: {ex.Message}");
            }
        }

        private async Task ApplyZoneChanges()
        {
            try
            {
                var zoneState = _zoneStates[_selectedZone];
                WriteDebugToFile($"🎨 ApplyZoneChanges - Zone: {_selectedZone}, Pattern: {zoneState.Pattern}, Color: {zoneState.Color}, Enabled: {zoneState.Enabled}");
                
                if (zoneState.Pattern == "Off" || !zoneState.Enabled)
                {
                    // Turn off the zone using Off pattern
                    WriteDebugToFile($"🎨 Turning off zone {_selectedZone}");
                    var result = await LedServiceManager.Instance.SetZonePatternAsync(_selectedZone, "Off", "Black", 0);
                    WriteDebugToFile($"🎨 Turn off result: Success={result.Success}, Message={result.Message}");
                }
                else
                {
                    // Apply pattern, color and brightness
                    WriteDebugToFile($"🎨 Setting zone {_selectedZone} to pattern {zoneState.Pattern} with color {zoneState.Color} and brightness {zoneState.Brightness}");
                    var result = await LedServiceManager.Instance.SetZonePatternAsync(_selectedZone, zoneState.Pattern, zoneState.Color, zoneState.Brightness);
                    WriteDebugToFile($"🎨 Set pattern result: Success={result.Success}, Message={result.Message}");
                }

                // Update visual feedback
                UpdateAllZoneVisuals();
            }
            catch (Exception ex)
            {
                WriteDebugToFile($"💥 Apply zone changes failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Apply zone changes failed: {ex.Message}");
            }
        }

        private void OnBackClicked(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }

        // Helper methods
        private static string GetZoneDisplayName(string zone)
        {
            return zone switch
            {
                "Skull" => "Skull",
                "BottomFront" => "Bottom Front",
                "BottomRight" => "Bottom Right", 
                "BottomLeft" => "Bottom Left",
                _ => zone
            };
        }

        private static string GetPatternDisplayName(string pattern)
        {
            return pattern switch
            {
                "Off" => "Off",
                "Solid" => "Solid",
                "Breathing" => "Breathing",
                "Pulse" => "Pulse",
                "Strobing" => "Strobing",
                "PulseTrain1" => "PulseTrain1",
                "PulseTrain2" => "PulseTrain2", 
                "PulseTrain3" => "PulseTrain3",
                "Rainbow" => "Rainbow",
                _ => pattern
            };
        }

        private static Windows.UI.Color StringToColor(string colorName)
        {
            return colorName.ToLower() switch
            {
                "red" => Windows.UI.Color.FromArgb(255, 255, 0, 0),
                "green" => Windows.UI.Color.FromArgb(255, 0, 255, 0),
                "blue" => Windows.UI.Color.FromArgb(255, 0, 0, 255),
                "yellow" => Windows.UI.Color.FromArgb(255, 255, 255, 0),
                "purple" => Windows.UI.Color.FromArgb(255, 128, 0, 128),
                "cyan" => Windows.UI.Color.FromArgb(255, 0, 255, 255),
                "white" => Windows.UI.Color.FromArgb(255, 255, 255, 255),
                "black" => Windows.UI.Color.FromArgb(255, 0, 0, 0),
                "orange" => Windows.UI.Color.FromArgb(255, 255, 165, 0),
                _ => Windows.UI.Color.FromArgb(255, 128, 128, 128)
            };
        }

        private static string GetColorNameFromBrush(SolidColorBrush? brush)
        {
            if (brush == null) return "Red";
            
            var color = brush.Color;
            
            // Match common colors to names
            if (color.R == 255 && color.G == 0 && color.B == 0) return "Red";
            if (color.R == 0 && color.G == 255 && color.B == 0) return "Green";
            if (color.R == 0 && color.G == 0 && color.B == 255) return "Blue";
            if (color.R == 255 && color.G == 255 && color.B == 0) return "Yellow";
            if (color.R == 128 && color.G == 0 && color.B == 128) return "Purple";
            if (color.R == 0 && color.G == 255 && color.B == 255) return "Cyan";
            if (color.R == 255 && color.G == 255 && color.B == 255) return "White";
            if (color.R == 0 && color.G == 0 && color.B == 0) return "Black";
            if (color.R == 255 && color.G == 165 && color.B == 0) return "Orange";
            
            return "Red"; // Default
        }
    }
}
