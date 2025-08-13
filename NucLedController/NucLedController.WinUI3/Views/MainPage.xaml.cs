using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace NucLedController.WinUI3.Views
{
    /// <summary>
    /// Intel NUC Software Studio - Main page with three feature tiles
    /// </summary>
    public partial class MainPage : Page
    {
        private DispatcherTimer _animationTimer;
        private Random _random = new Random();

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += OnPageLoaded;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            StartGaugeAnimations();
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
