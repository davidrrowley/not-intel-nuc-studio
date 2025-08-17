using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;

namespace NotIntelNucStudio.WinUI3.Controls
{
    public sealed partial class CircularGauge : UserControl
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(CircularGauge), new PropertyMetadata(0.0, OnValueChanged));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(double), typeof(CircularGauge), new PropertyMetadata(100.0, OnValueChanged));

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register("Unit", typeof(string), typeof(CircularGauge), new PropertyMetadata(""));

        public static readonly DependencyProperty StrokeColorProperty =
            DependencyProperty.Register("StrokeColor", typeof(Brush), typeof(CircularGauge), new PropertyMetadata(null, OnStrokeColorChanged));

        public static readonly DependencyProperty TemperatureProperty =
            DependencyProperty.Register("Temperature", typeof(double), typeof(CircularGauge), new PropertyMetadata(0.0, OnTemperatureChanged));

        public static readonly DependencyProperty ShowTemperatureProperty =
            DependencyProperty.Register("ShowTemperature", typeof(bool), typeof(CircularGauge), new PropertyMetadata(false, OnShowTemperatureChanged));

        public CircularGauge()
        {
            this.InitializeComponent();
            this.SizeChanged += CircularGauge_SizeChanged;
            this.Loaded += CircularGauge_Loaded;
        }

        private void CircularGauge_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateCanvasSize();
            UpdateDisplay();
        }

        private void CircularGauge_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCanvasSize();
            UpdateDisplay();
        }

        private void UpdateCanvasSize()
        {
            if (MainCanvas != null && ActualWidth > 0 && ActualHeight > 0)
            {
                double size = Math.Min(ActualWidth, ActualHeight);
                MainCanvas.Width = size;
                MainCanvas.Height = size;
                
                // Update background circle
                if (BackgroundCircle != null)
                {
                    double strokeThickness = size * 0.06; // 6% of size
                    double circleSize = size - strokeThickness;
                    double offset = strokeThickness / 2;
                    
                    BackgroundCircle.Width = circleSize;
                    BackgroundCircle.Height = circleSize;
                    BackgroundCircle.StrokeThickness = strokeThickness;
                    Canvas.SetLeft(BackgroundCircle, offset);
                    Canvas.SetTop(BackgroundCircle, offset);
                }
                
                // Update progress arc stroke thickness
                if (ProgressArc != null)
                {
                    ProgressArc.StrokeThickness = size * 0.06;
                }
            }
        }

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public double Maximum
        {
            get { return (double)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public string Unit
        {
            get { return (string)GetValue(UnitProperty); }
            set { SetValue(UnitProperty, value); }
        }

        public Brush StrokeColor
        {
            get { return (Brush)GetValue(StrokeColorProperty); }
            set { SetValue(StrokeColorProperty, value); }
        }

        public double Temperature
        {
            get { return (double)GetValue(TemperatureProperty); }
            set { SetValue(TemperatureProperty, value); }
        }

        public bool ShowTemperature
        {
            get { return (bool)GetValue(ShowTemperatureProperty); }
            set { SetValue(ShowTemperatureProperty, value); }
        }

        public string ValuePercentageText
        {
            get
            {
                if (Maximum == 0) return "0%";
                double percentage = (Value / Maximum) * 100;
                return $"{percentage:F0}%";
            }
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CircularGauge)d;
            control.UpdateDisplay();
        }

        private static void OnStrokeColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CircularGauge)d;
            control.UpdateStrokeColor();
        }

        private static void OnTemperatureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CircularGauge)d;
            control.UpdateTemperatureDisplay();
        }

        private static void OnShowTemperatureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CircularGauge)d;
            control.UpdateTemperatureVisibility();
        }

        private void UpdateDisplay()
        {
            if (ProgressArc != null && ActualWidth > 0)
            {
                double percentage = Maximum == 0 ? 0 : (Value / Maximum);
                percentage = Math.Max(0, Math.Min(1, percentage)); // Clamp between 0 and 1
                
                // Create the arc path
                var pathGeometry = CreateArcPath(percentage);
                ProgressArc.Data = pathGeometry;
            }

            if (ValueText != null)
            {
                ValueText.Text = ValuePercentageText;
            }

            if (UnitText != null)
            {
                UnitText.Text = Unit;
            }

            // Update temperature display
            UpdateTemperatureDisplay();
            UpdateTemperatureVisibility();
        }

        private void UpdateStrokeColor()
        {
            if (ProgressArc != null && StrokeColor != null)
            {
                ProgressArc.Stroke = StrokeColor;
            }
        }

        private PathGeometry CreateArcPath(double percentage)
        {
            var pathGeometry = new PathGeometry();
            
            if (percentage <= 0 || ActualWidth <= 0) return pathGeometry;

            var pathFigure = new PathFigure();
            
            // Circle parameters based on actual size
            double size = Math.Min(ActualWidth, ActualHeight);
            double centerX = size / 2;
            double centerY = size / 2;
            double strokeThickness = size * 0.06;
            double radius = (size - strokeThickness) / 2;
            
            // Calculate angles (start from top, -90 degrees)
            double startAngle = -90;
            double endAngle = startAngle + (percentage * 360);
            
            // Convert to radians
            double startRadians = startAngle * Math.PI / 180;
            double endRadians = endAngle * Math.PI / 180;
            
            // Calculate start and end points
            double startX = centerX + radius * Math.Cos(startRadians);
            double startY = centerY + radius * Math.Sin(startRadians);
            double endX = centerX + radius * Math.Cos(endRadians);
            double endY = centerY + radius * Math.Sin(endRadians);
            
            pathFigure.StartPoint = new Windows.Foundation.Point(startX, startY);
            
            // Create arc segment
            var arcSegment = new ArcSegment
            {
                Point = new Windows.Foundation.Point(endX, endY),
                Size = new Windows.Foundation.Size(radius, radius),
                IsLargeArc = percentage > 0.5,
                SweepDirection = SweepDirection.Clockwise
            };
            
            pathFigure.Segments.Add(arcSegment);
            pathGeometry.Figures.Add(pathFigure);
            
            return pathGeometry;
        }

        private void UpdateTemperatureDisplay()
        {
            if (TemperatureText != null)
            {
                TemperatureText.Text = $"{Temperature:F0}°C";
            }
        }

        private void UpdateTemperatureVisibility()
        {
            if (TemperatureText != null)
            {
                TemperatureText.Visibility = ShowTemperature ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
