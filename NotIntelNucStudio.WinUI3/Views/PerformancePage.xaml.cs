using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace NotIntelNucStudio.WinUI3.Views
{
    public sealed partial class PerformancePage : Page
    {
        private DispatcherTimer _animationTimer;
        private Random _random = new Random();

        public PerformancePage()
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
            _animationTimer.Interval = TimeSpan.FromSeconds(2);
            _animationTimer.Tick += OnAnimationTick;
            _animationTimer.Start();
        }

        private void OnAnimationTick(object sender, object e)
        {
            // Animate the gauges to simulate real-time monitoring
            CpuGauge.Value = Math.Max(15, Math.Min(80, CpuGauge.Value + _random.Next(-5, 5)));
            CpuGaugeSmall.Value = CpuGauge.Value;

            GpuGauge.Value = Math.Max(0, Math.Min(60, GpuGauge.Value + _random.Next(-3, 3)));
            
            MemoryGauge.Value = Math.Max(30, Math.Min(90, MemoryGauge.Value + _random.Next(-8, 8)));
            
            StorageGauge.Value = Math.Max(85, Math.Min(98, StorageGauge.Value + _random.Next(-2, 2)));
        }

        private void OnBackClicked(object sender, RoutedEventArgs e)
        {
            if (_animationTimer != null)
            {
                _animationTimer.Stop();
                _animationTimer = null;
            }
            Frame.GoBack();
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
