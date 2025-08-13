using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Windows.UI;
using NucLedController.WinUI3.Helpers;

namespace NucLedController.WinUI3.Views
{
    /// <summary>
    /// Intel NUC Software Studio - Main page with three feature tiles
    /// </summary>
    public partial class MainPage : Page
    {
        private DispatcherTimer _animationTimer;
        private Random _random = new Random();
        private List<Border> _gaugeElements;
        private List<Border> _nucElements;

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += OnPageLoaded;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            InitializeGaugeElements();
            InitializeNucElements();
            InitializeLedOverlays();
            StartGaugeAnimations();
            // Initial layout
            ConfigureResponsiveLayout();
            ConfigureResponsiveNucLayout();
        }

        private async void InitializeLedOverlays()
        {
            try
            {
                // Create bright yellow LED overlays using the mask images
                var yellowColor = LedMaskHelper.FromHex("#FFFF00");
                
                // Front view LED overlay
                var frontOverlay = await LedMaskHelper.CreateColoredLedOverlayAsync(
                    "ms-appx:///Assets/NucImages/nuc_front_mask.png", yellowColor);
                if (frontOverlay != null && MainFrontLedOverlayImage != null)
                {
                    MainFrontLedOverlayImage.Source = frontOverlay;
                }
                
                // Left view LED overlay  
                var leftOverlay = await LedMaskHelper.CreateColoredLedOverlayAsync(
                    "ms-appx:///Assets/NucImages/nuc_left_mask.png", yellowColor);
                if (leftOverlay != null && MainLeftLedOverlayImage != null)
                {
                    MainLeftLedOverlayImage.Source = leftOverlay;
                }
                
                // Right view LED overlay
                var rightOverlay = await LedMaskHelper.CreateColoredLedOverlayAsync(
                    "ms-appx:///Assets/NucImages/nuc_right_mask.png", yellowColor);
                if (rightOverlay != null && MainRightLedOverlayImage != null)
                {
                    MainRightLedOverlayImage.Source = rightOverlay;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing LED overlays: {ex.Message}");
            }
        }

        private void InitializeGaugeElements()
        {
            _gaugeElements = new List<Border>
            {
                CpuGaugeBorder,
                DgpuGaugeBorder,
                MemoryGaugeBorder,
                StorageGaugeBorder,
                SystemTempGaugeBorder
            };
        }

        private void InitializeNucElements()
        {
            _nucElements = new List<Border>
            {
                FrontNucBorder,
                LeftNucBorder,
                RightNucBorder
            };
        }

        private void GaugesScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ConfigureResponsiveLayout();
        }

        private void NucScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ConfigureResponsiveNucLayout();
        }

        private void ConfigureResponsiveLayout()
        {
            if (GaugesScrollViewer == null || ResponsiveGaugeGrid == null || _gaugeElements == null)
                return;

            var availableWidth = GaugesScrollViewer.ActualWidth - 10; // Reduced padding
            var availableHeight = GaugesScrollViewer.ActualHeight - 10;
            
            // Each gauge now needs 170px (160px + 10px margin)
            const double gaugeWidth = 170;
            const double gaugeHeight = 170;
            
            // Calculate how many columns can actually fit
            var maxColumns = Math.Max(1, (int)(availableWidth / gaugeWidth));
            var maxRows = Math.Max(1, (int)(availableHeight / gaugeHeight));
            
            // Determine best arrangement for 5 gauges with more gradual transitions
            int columns, rows;
            
            if (maxColumns >= 5)
            {
                // Wide layout: all in one row (5×1)
                columns = 5;
                rows = 1;
            }
            else if (maxColumns >= 4)
            {
                // Still fairly wide: 4×2 arrangement (4 on top, 1 on bottom)
                columns = 4;
                rows = 2;
            }
            else if (maxColumns >= 3)
            {
                // Medium layout: 3×2 arrangement (3 on top, 2 on bottom)
                columns = 3;
                rows = 2;
            }
            else if (maxColumns >= 2)
            {
                // Getting narrower: 2×3 arrangement (2×2×1)
                columns = 2;
                rows = 3;
            }
            else
            {
                // Very narrow: single column (1×5)
                columns = 1;
                rows = 5;
            }

            // Clear existing grid definitions
            ResponsiveGaugeGrid.ColumnDefinitions.Clear();
            ResponsiveGaugeGrid.RowDefinitions.Clear();

            // Add column definitions
            for (int i = 0; i < columns; i++)
            {
                ResponsiveGaugeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            }

            // Add row definitions
            for (int i = 0; i < rows; i++)
            {
                ResponsiveGaugeGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            }

            // Position gauge elements
            for (int i = 0; i < _gaugeElements.Count; i++)
            {
                var element = _gaugeElements[i];
                var row = i / columns;
                var col = i % columns;

                Grid.SetRow(element, row);
                Grid.SetColumn(element, col);
            }
        }

        private void ConfigureResponsiveNucLayout()
        {
            if (NucScrollViewer == null || ResponsiveNucGrid == null || _nucElements == null)
                return;

            var availableWidth = NucScrollViewer.ActualWidth - 20; // Account for padding
            var availableHeight = NucScrollViewer.ActualHeight - 20;
            
            // Base NUC container size
            const double baseNucWidth = 240;
            const double baseNucHeight = 200;
            
            // Calculate scaling factor based on available space
            var widthScale = Math.Min(1.5, Math.Max(0.6, availableWidth / (baseNucWidth * 3.2))); // 3.2 accounts for 3 containers + spacing
            var heightScale = Math.Min(1.5, Math.Max(0.6, availableHeight / (baseNucHeight * 1.2)));
            
            // Use the smaller scale to maintain aspect ratio
            var scale = Math.Min(widthScale, heightScale);
            
            // Apply scaling to each NUC container
            foreach (var nucContainer in _nucElements)
            {
                if (nucContainer != null)
                {
                    nucContainer.Width = baseNucWidth * scale;
                    nucContainer.Height = baseNucHeight * scale;
                }
            }
        }

        private void StartGaugeAnimations()
        {
            _animationTimer = new DispatcherTimer();
            _animationTimer.Interval = TimeSpan.FromSeconds(3);
            _animationTimer.Tick += OnAnimationTick;
            _animationTimer.Start();
        }

        private void OnAnimationTick(object sender, object e)
        {
            // Animate main page gauges to simulate real-time monitoring
            if (MainCpuGauge != null)
            {
                MainCpuGauge.Value = Math.Max(15, Math.Min(80, MainCpuGauge.Value + _random.Next(-5, 8)));
            }

            if (MainSystemCpuGauge != null)
            {
                MainSystemCpuGauge.Value = Math.Max(15, Math.Min(80, MainSystemCpuGauge.Value + _random.Next(-5, 8)));
            }

            if (MainSystemGpuGauge != null)
            {
                MainSystemGpuGauge.Value = Math.Max(0, Math.Min(60, MainSystemGpuGauge.Value + _random.Next(-3, 5)));
            }

            if (MainSystemMemoryGauge != null)
            {
                MainSystemMemoryGauge.Value = Math.Max(30, Math.Min(85, MainSystemMemoryGauge.Value + _random.Next(-5, 7)));
            }

            if (MainSystemStorageGauge != null)
            {
                MainSystemStorageGauge.Value = Math.Max(85, Math.Min(98, MainSystemStorageGauge.Value + _random.Next(-2, 3)));
            }

            // Animate LED preview with subtle color cycling
            AnimateLedPreview();
        }

        private void AnimateLedPreview()
        {
            // TEMPORARILY DISABLED TO TEST HARDCODED COLORS
            /*
            // Create dynamic LED colors for the mask-based system
            if (EffectsToggle?.IsOn == true)
            {
                // Generate random colors for animation
                var colors = new[] { "Red", "Green", "Blue", "Yellow", "Purple", "Cyan", "Orange" };
                var randomColor = colors[_random.Next(colors.Length)];
                var colorBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(GetColorFromName(randomColor));
                
                // Apply colors to LED mask overlays with proper opacity
                if (MainFrontBottomLedOverlay != null) 
                {
                    MainFrontBottomLedOverlay.Fill = colorBrush;
                    MainFrontBottomLedOverlay.Opacity = 0.85;
                }
                if (MainBottomLeftLedOverlay != null) 
                {
                    MainBottomLeftLedOverlay.Fill = colorBrush;
                    MainBottomLeftLedOverlay.Opacity = 0.85;
                }
                if (MainBottomRightLedOverlay != null) 
                {
                    MainBottomRightLedOverlay.Fill = colorBrush;
                    MainBottomRightLedOverlay.Opacity = 0.85;
                }
            }
            else
            {
                // Reset to default/hidden when effects are off
                var defaultBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 68, 68, 68));
                
                if (MainFrontBottomLedOverlay != null) 
                {
                    MainFrontBottomLedOverlay.Fill = defaultBrush;
                    MainFrontBottomLedOverlay.Opacity = 0.3;
                }
                if (MainBottomLeftLedOverlay != null) 
                {
                    MainBottomLeftLedOverlay.Fill = defaultBrush;
                    MainBottomLeftLedOverlay.Opacity = 0.3;
                }
                if (MainBottomRightLedOverlay != null) 
                {
                    MainBottomRightLedOverlay.Fill = defaultBrush;
                    MainBottomRightLedOverlay.Opacity = 0.3;
                }
            }
            */
        }
        
        private Color GetColorFromName(string colorName)
        {
            return colorName switch
            {
                "Red" => Color.FromArgb(255, 255, 0, 0),
                "Green" => Color.FromArgb(255, 0, 255, 0),
                "Blue" => Color.FromArgb(255, 0, 0, 255),
                "Yellow" => Color.FromArgb(255, 255, 255, 0),
                "Purple" => Color.FromArgb(255, 128, 0, 128),
                "Cyan" => Color.FromArgb(255, 0, 255, 255),
                "Orange" => Color.FromArgb(255, 255, 165, 0),
                _ => Color.FromArgb(255, 128, 128, 128)
            };
        }

        private void OnLedsClicked(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LedsPage));
        }

        private void OnPerformanceClicked(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(PerformancePage));
        }

        private void OnSystemMonitorClicked(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SystemMonitorPage));
        }

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            if (_animationTimer != null)
            {
                _animationTimer.Stop();
                _animationTimer = null;
            }
            base.OnNavigatedFrom(e);
        }
    }
}
