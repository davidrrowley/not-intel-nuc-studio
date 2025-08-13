using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

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

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += OnPageLoaded;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            InitializeGaugeElements();
            StartGaugeAnimations();
            // Initial layout
            ConfigureResponsiveLayout();
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

        private void GaugesScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ConfigureResponsiveLayout();
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
