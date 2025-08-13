using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace NucLedController.WinUI3.Views
{
    public sealed partial class SystemMonitorPage : Page
    {
        private DispatcherTimer _animationTimer;
        private Random _random = new Random();

        public SystemMonitorPage()
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
            // Animate the gauges to simulate real-time monitoring
            CpuGauge.Value = Math.Max(20, Math.Min(70, CpuGauge.Value + _random.Next(-8, 8)));
            
            GpuGauge.Value = Math.Max(0, Math.Min(45, GpuGauge.Value + _random.Next(-2, 3)));
            
            MemoryGauge.Value = Math.Max(35, Math.Min(85, MemoryGauge.Value + _random.Next(-5, 5)));
            
            StorageGauge.Value = Math.Max(88, Math.Min(95, StorageGauge.Value + _random.Next(-1, 1)));
        }

        private void OnBackClicked(object sender, RoutedEventArgs e)
        {
            if (_animationTimer != null)
            {
                _animationTimer.Stop();
                _animationTimer = null;
            }
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
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
