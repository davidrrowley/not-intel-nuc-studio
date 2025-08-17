using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.UI;
using Windows.Storage.Streams;
using Windows.Storage;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using NotIntelNucStudio.WinUI3.Helpers;
using System.Threading.Tasks;
using NotIntelNucStudio.WinUI3.Services;
using LibreHardwareMonitor.Hardware;

#nullable enable

namespace NotIntelNucStudio.WinUI3.Views
{
    /// <summary>
    /// Intel NUC Software Studio - Main page with three feature tiles
    /// </summary>
    public partial class MainPage : Page
    {
        private DispatcherTimer? _animationTimer;
        private Random _random = new Random();
        private List<Border>? _nucElements;
        private List<Border>? _gaugeElements;
        private bool _effectsEnabled = false;
        private bool _userToggling = false; // Prevent recursive toggle events
        private bool _commandInProgress = false; // Prevent concurrent commands
        private LibreHardwareService? _hardwareService; // Hardware monitoring service
        private int _systemInfoUpdateCounter = 0; // Counter for periodic system info updates

        public MainPage()
        {
            WriteDebugToFile("🚀 MainPage constructor called!");
            this.InitializeComponent();
            this.Loaded += OnPageLoaded;
            WriteDebugToFile("🔗 Loaded event handler attached!");
        }

        private static void WriteDebugToFile(string message)
        {
            try
            {
                var debugFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "logs", "NucLedDebug.txt");
                var logsDirectory = Path.GetDirectoryName(debugFile);
                if (logsDirectory != null && !Directory.Exists(logsDirectory))
                {
                    Directory.CreateDirectory(logsDirectory);
                }
                File.AppendAllText(debugFile, $"{DateTime.Now:HH:mm:ss.fff} - {message}\n");
            }
            catch { /* ignore file errors */ }
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            WriteDebugToFile("📱 OnPageLoaded event fired!");
            InitializeGaugeElements();
            InitializeNucElements();
            InitializeLedOverlays();
            StartGaugeAnimations();
            // Initial layout
            ConfigureResponsiveLayout();
            ConfigureResponsiveNucLayout();
            
            // Auto-connect to service and setup Effects toggle
            _ = InitializeServiceConnectionAsync();
            
            WriteDebugToFile("✅ OnPageLoaded completed!");
        }

        private async Task InitializeServiceConnectionAsync()
        {
            try
            {
                WriteDebugToFile("🔌 Initializing NINLCS service connection via singleton...");
                
                // Test connection and get current state
                var connectResult = await LedServiceManager.Instance.EnsureConnectedAsync();
                
                if (connectResult.Success)
                {
                    WriteDebugToFile("✅ NINLCS service connected successfully via singleton");
                    
                    // Initialize hardware monitoring service in background to avoid UI blocking
                    WriteDebugToFile("🔧 Starting hardware monitoring service initialization in background...");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            WriteDebugToFile("🔧 Initializing hardware monitoring service...");
                            var tempHardwareService = new LibreHardwareService();
                            var hwInitResult = await tempHardwareService.InitializeAsync();
                            
                            // Update on UI thread
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                if (hwInitResult)
                                {
                                    _hardwareService = tempHardwareService;
                                    WriteDebugToFile("✅ Hardware monitoring service initialized successfully (background)");
                                }
                                else
                                {
                                    WriteDebugToFile("⚠️ Hardware monitoring service initialization failed - will use mock data");
                                    _hardwareService = null;
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            WriteDebugToFile($"❌ Background hardware service initialization failed: {ex.Message}");
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                _hardwareService = null;
                            });
                        }
                    });
                    
                    // Continue with LED initialization immediately (don't wait for hardware service)
                    var statusResult = await LedServiceManager.Instance.GetStatusAsync();
                    if (statusResult.Success)
                    {
                        // For now, assume LEDs are off initially - we can improve this later
                        _effectsEnabled = false;
                        
                        // Update UI on main thread
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (EffectsToggle != null)
                            {
                                // Remove any existing event handler first to prevent duplicates
                                EffectsToggle.Toggled -= OnEffectsToggled;
                                
                                _userToggling = true;
                                EffectsToggle.IsOn = _effectsEnabled;
                                _userToggling = false;
                                
                                // Now attach event handler
                                EffectsToggle.Toggled += OnEffectsToggled;
                                WriteDebugToFile($"🎯 Effects toggle synced to hardware state: {_effectsEnabled}");
                            }
                        });
                        
                        await UpdateLedOverlaysAsync(_effectsEnabled);
                    }
                }
                else
                {
                    WriteDebugToFile($"❌ NINLCS service connection failed: {connectResult.Message}");
                }
            }
            catch (Exception ex)
            {
                WriteDebugToFile($"🔥 NINLCS service initialization error: {ex.Message}");
            }
        }

        private async void OnEffectsToggled(object sender, RoutedEventArgs e)
        {
            if (_userToggling || _commandInProgress) 
            {
                WriteDebugToFile($"⏸️ Toggle blocked - userToggling:{_userToggling}, inProgress:{_commandInProgress}");
                return;
            }
            
            var isOn = EffectsToggle?.IsOn ?? false;
            WriteDebugToFile($"🎛️ Effects toggle clicked: {isOn}");
            
            _commandInProgress = true;
            
            try
            {
                if (isOn)
                {
                    WriteDebugToFile("🔆 Sending SetAllZones command to NINLCS service...");
                    // Turn on all zones with solid blue color at 100% brightness
                    var result = await LedServiceManager.Instance.SetAllZonesAsync("Solid", "Blue", 100, false);
                    if (result.Success)
                    {
                        _effectsEnabled = true;
                        WriteDebugToFile("✅ LEDs turned ON successfully");
                        await UpdateLedOverlaysAsync(true);
                    }
                    else
                    {
                        WriteDebugToFile($"❌ TurnOn failed: {result.Message}");
                        // Revert toggle if command failed
                        _userToggling = true;
                        EffectsToggle.IsOn = false;
                        _userToggling = false;
                    }
                }
                else
                {
                    WriteDebugToFile("🔅 Sending TurnOffAll command to NINLCS service...");
                    var result = await LedServiceManager.Instance.TurnOffAllAsync();
                    if (result.Success)
                    {
                        _effectsEnabled = false;
                        WriteDebugToFile("✅ LEDs turned OFF successfully");
                        await UpdateLedOverlaysAsync(false);
                    }
                    else
                    {
                        WriteDebugToFile($"❌ TurnOff failed: {result.Message}");
                        // Revert toggle if command failed
                        _userToggling = true;
                        EffectsToggle.IsOn = true;
                        _userToggling = false;
                    }
                }
            }
            catch (Exception ex)
            {
                WriteDebugToFile($"🔥 Toggle error: {ex.Message}");
                // Revert toggle on error
                _userToggling = true;
                EffectsToggle.IsOn = !isOn;
                _userToggling = false;
            }
            finally
            {
                _commandInProgress = false;
                WriteDebugToFile($"🏁 Toggle operation completed");
            }
        }

        private async Task UpdateLedOverlaysAsync(bool effectsOn)
        {
            try
            {
                WriteDebugToFile($"🎨 Updating LED overlays - Effects: {effectsOn}");
                
                // Find all LED overlay images
                var overlayImages = new[]
                {
                    this.FindName("MainFrontLedOverlayImage") as Image,
                    this.FindName("MainLeftLedOverlayImage") as Image,
                    this.FindName("MainRightLedOverlayImage") as Image
                };

                foreach (var image in overlayImages)
                {
                    if (image != null)
                    {
                        // Update visibility/opacity based on effects state
                        image.Opacity = effectsOn ? 0.9 : 0.3;
                        WriteDebugToFile($"🔅 Updated overlay opacity to {image.Opacity}");
                    }
                }
            }
            catch (Exception ex)
            {
                WriteDebugToFile($"🔥 Error updating LED overlays: {ex.Message}");
            }
        }

        private async void InitializeLedOverlays()
        {
            try
            {
                WriteDebugToFile("🔍 Starting LED overlay initialization...");
                
                // Define LED mask configurations with correct file paths
                var ledConfigs = new[]
                {
                    new { Name = "MainFrontLedOverlayImage", MaskPath = "ms-appx:///Assets/NucImages/nuc_front_mask.png", Color = Color.FromArgb(255, 255, 255, 0) }, // Bright Yellow
                    new { Name = "MainLeftLedOverlayImage", MaskPath = "ms-appx:///Assets/NucImages/nuc_left_mask.png", Color = Color.FromArgb(255, 255, 255, 0) },   // Bright Yellow  
                    new { Name = "MainRightLedOverlayImage", MaskPath = "ms-appx:///Assets/NucImages/nuc_right_mask.png", Color = Color.FromArgb(255, 255, 255, 0) } // Bright Yellow
                };

                foreach (var config in ledConfigs)
                {
                    try
                    {
                        WriteDebugToFile($"🎯 Processing {config.Name} with mask {config.MaskPath}");
                        
                        // Find the Image control
                        var imageControl = this.FindName(config.Name) as Image;
                        if (imageControl == null)
                        {
                            WriteDebugToFile($"✗ {config.Name} not found in XAML");
                            continue;
                        }
                        
                        WriteDebugToFile($"✓ Found {config.Name} image control");
                        
                        // Try to create LED overlay using improved mask processing
                        WriteDebugToFile($"🎨 Calling LedMaskHelper.CreateColoredLedOverlayAsync...");
                        var ledOverlay = await LedMaskHelper.CreateColoredLedOverlayAsync(config.MaskPath, config.Color, threshold: 16);
                        
                        if (ledOverlay != null)
                        {
                            imageControl.Source = ledOverlay;
                            imageControl.Opacity = 0.9; // High visibility for testing
                            WriteDebugToFile($"✅ {config.Name} mask-based LED overlay applied successfully");
                        }
                        else
                        {
                            WriteDebugToFile($"❌ LedMaskHelper returned null for {config.Name}");
                            
                            // FALLBACK: Create yellow test overlay if even improved processing fails
                            var fallbackOverlay = new WriteableBitmap(100, 100);
                            var buffer = fallbackOverlay.PixelBuffer.ToArray();
                            for (int i = 0; i < buffer.Length; i += 4)
                            {
                                buffer[i] = 0;     // Blue
                                buffer[i + 1] = 255; // Green  
                                buffer[i + 2] = 255; // Red (Yellow = Red + Green)
                                buffer[i + 3] = 255; // Alpha
                            }
                            
                            using (var stream = fallbackOverlay.PixelBuffer.AsStream())
                            {
                                stream.Position = 0;
                                await stream.WriteAsync(buffer, 0, buffer.Length);
                            }
                            fallbackOverlay.Invalidate();
                            
                            imageControl.Source = fallbackOverlay;
                            imageControl.Opacity = 1.0;
                            WriteDebugToFile($"🟡 {config.Name} fallback yellow overlay applied");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"💥 Error processing {config.Name}: {ex.Message}");
                        Console.WriteLine($"   Stack trace: {ex.StackTrace}");
                    }
                }
                
                Console.WriteLine("🎯 LED overlay initialization completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Critical error: {ex.Message}");
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

            var availableWidth = NucScrollViewer.ActualWidth - 20;
            var availableHeight = NucScrollViewer.ActualHeight - 20;
            
            // Each NUC container now needs 366px (350px + 16px margin)
            const double nucWidth = 366;
            const double nucHeight = 366;
            
            // Calculate how many columns can fit
            var maxColumns = Math.Max(1, (int)(availableWidth / nucWidth));
            var maxRows = Math.Max(1, (int)(availableHeight / nucHeight));
            
            // Determine best arrangement for 3 NUC views
            int columns, rows;
            
            if (maxColumns >= 3)
            {
                // Wide layout: all in one row (3×1)
                columns = 3;
                rows = 1;
            }
            else if (maxColumns >= 2)
            {
                // Medium layout: 2×2 arrangement (2 on top, 1 on bottom)
                columns = 2;
                rows = 2;
            }
            else
            {
                // Narrow layout: single column (1×3)
                columns = 1;
                rows = 3;
            }

            // Clear existing grid definitions
            ResponsiveNucGrid.ColumnDefinitions.Clear();
            ResponsiveNucGrid.RowDefinitions.Clear();

            // Add column definitions
            for (int i = 0; i < columns; i++)
            {
                ResponsiveNucGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            }

            // Add row definitions
            for (int i = 0; i < rows; i++)
            {
                ResponsiveNucGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            }

            // Position NUC elements
            for (int i = 0; i < _nucElements.Count; i++)
            {
                var element = _nucElements[i];
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

        private async void OnAnimationTick(object sender, object e)
        {
            // Use real hardware data if service is available, otherwise fall back to mock data
            if (_hardwareService != null)
            {
                // Log first successful hardware data fetch
                if (_systemInfoUpdateCounter == 0)
                {
                    WriteDebugToFile("🎯 Hardware monitoring service is now active - switching from mock to real data");
                }
                
                try
                {
                    // Get system metrics
                    var metrics = await _hardwareService.GetSystemMetricsAsync();
                    
                    // Update CPU gauge with real data
                    if (MainSystemCpuGauge != null && metrics.ContainsKey("CpuUsage"))
                    {
                        var cpuUsage = Convert.ToDouble(metrics["CpuUsage"]);
                        MainSystemCpuGauge.Value = Math.Max(0, Math.Min(100, cpuUsage));
                    }

                    // Update memory gauge with real data
                    if (MainSystemMemoryGauge != null && metrics.ContainsKey("MemoryUsage"))
                    {
                        var memUsage = Convert.ToDouble(metrics["MemoryUsage"]);
                        MainSystemMemoryGauge.Value = Math.Max(0, Math.Min(100, memUsage));
                    }

                    // Get GPU data separately
                    var gpuSensors = await _hardwareService.GetSensorsByHardwareAsync(LibreHardwareMonitor.Hardware.HardwareType.GpuNvidia);
                    if (!gpuSensors.Any())
                    {
                        gpuSensors = await _hardwareService.GetSensorsByHardwareAsync(LibreHardwareMonitor.Hardware.HardwareType.GpuAmd);
                    }
                    if (!gpuSensors.Any())
                    {
                        gpuSensors = await _hardwareService.GetSensorsByHardwareAsync(LibreHardwareMonitor.Hardware.HardwareType.GpuIntel);
                    }

                    // Update GPU gauge with real data
                    if (MainSystemGpuGauge != null)
                    {
                        var gpuLoad = gpuSensors.FirstOrDefault(s => s.Type == LibreHardwareMonitor.Hardware.SensorType.Load && 
                                                                     s.Name.Contains("GPU Core"));
                        if (gpuLoad != null)
                        {
                            MainSystemGpuGauge.Value = Math.Max(0, Math.Min(100, gpuLoad.Value));
                        }
                        else
                        {
                            // Keep existing value if no GPU data available
                            MainSystemGpuGauge.Value = Math.Max(0, Math.Min(60, MainSystemGpuGauge.Value + _random.Next(-3, 5)));
                        }
                    }

                    // Storage usage estimation (this requires more complex logic, using placeholder for now)
                    if (MainSystemStorageGauge != null)
                    {
                        // For now, keep mock behavior for storage as real storage monitoring needs different approach
                        MainSystemStorageGauge.Value = Math.Max(85, Math.Min(98, MainSystemStorageGauge.Value + _random.Next(-2, 3)));
                    }

                    // Update main CPU gauge (duplicate of system CPU for now)
                    if (MainCpuGauge != null && metrics.ContainsKey("CpuUsage"))
                    {
                        var cpuUsage = Convert.ToDouble(metrics["CpuUsage"]);
                        MainCpuGauge.Value = Math.Max(0, Math.Min(100, cpuUsage));
                    }

                    // Update fan speeds with real data
                    await UpdateFanSpeedsAsync();

                    // Update system info on first run or every 60 seconds (20 cycles * 3 seconds)
                    _systemInfoUpdateCounter++;
                    if (_systemInfoUpdateCounter == 1 || _systemInfoUpdateCounter >= 20)
                    {
                        await UpdateSystemInfoAsync();
                        _systemInfoUpdateCounter = 0;
                    }
                }
                catch (Exception ex)
                {
                    WriteDebugToFile($"⚠️ Error updating hardware gauges: {ex.Message}");
                    // Fall back to mock data on error
                    UpdateGaugesWithMockData();
                }
            }
            else
            {
                // Hardware service not available, use mock data
                UpdateGaugesWithMockData();
            }

            // Animate LED preview with subtle color cycling
            AnimateLedPreview();
        }

        private void UpdateGaugesWithMockData()
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
            try
            {
                WriteDebugToFile("🔧 LED button clicked - navigating to LedsPage");
                bool result = Frame.Navigate(typeof(LedsPage));
                WriteDebugToFile($"🔧 Navigation result: {result}");
            }
            catch (Exception ex)
            {
                WriteDebugToFile($"💥 Navigation error: {ex.Message}");
                WriteDebugToFile($"💥 Stack trace: {ex.StackTrace}");
            }
        }

        private void OnPerformanceClicked(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(PerformancePage));
        }

        private void OnSystemMonitorClicked(object sender, RoutedEventArgs e)
        {
            // TODO: Re-enable SystemMonitorPage when XAML compilation issues are resolved
            // Frame.Navigate(typeof(SystemMonitorPage));
            Console.WriteLine("System Monitor navigation temporarily disabled");
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

        private async Task UpdateFanSpeedsAsync()
        {
            if (_hardwareService == null) return;

            try
            {
                var fanSensors = await _hardwareService.GetSensorsByTypeAsync(SensorType.Fan);
                var fanList = fanSensors.OrderBy(f => f.Name).ToList();

                if (fanList.Count > 0 && CpuFanSpeedText != null)
                {
                    var cpuFan = fanList.FirstOrDefault(f => f.Name.Contains("CPU") || f.Name.Contains("cpu"));
                    if (cpuFan != null)
                    {
                        CpuFanSpeedText.Text = $"CPU Fan: {cpuFan.Value:F0} RPM";
                    }
                    else if (fanList.Count > 0)
                    {
                        CpuFanSpeedText.Text = $"CPU Fan: {fanList[0].Value:F0} RPM";
                    }
                }

                if (fanList.Count > 1 && SystemFan1SpeedText != null)
                {
                    var systemFan1 = fanList.Skip(1).FirstOrDefault();
                    if (systemFan1 != null)
                    {
                        SystemFan1SpeedText.Text = $"System Fan 1: {systemFan1.Value:F0} RPM";
                    }
                }

                if (fanList.Count > 2 && SystemFan2SpeedText != null)
                {
                    var systemFan2 = fanList.Skip(2).FirstOrDefault();
                    if (systemFan2 != null)
                    {
                        SystemFan2SpeedText.Text = $"System Fan 2: {systemFan2.Value:F0} RPM";
                    }
                }
            }
            catch (Exception ex)
            {
                WriteDebugToFile($"⚠️ Error updating fan speeds: {ex.Message}");
            }
        }

        private async Task UpdateSystemInfoAsync()
        {
            if (_hardwareService == null) return;

            try
            {
                var systemInfo = await _hardwareService.GetSystemInfoAsync();

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (systemInfo.ContainsKey("SystemModel") && SystemModelText != null)
                    {
                        SystemModelText.Text = $"System Model: {systemInfo["SystemModel"]}";
                    }

                    if (systemInfo.ContainsKey("SystemManufacturer") && SystemManufacturerText != null)
                    {
                        SystemManufacturerText.Text = $"System Manufacturer: {systemInfo["SystemManufacturer"]}";
                    }

                    if (systemInfo.ContainsKey("BiosVersion") && BiosVersionText != null)
                    {
                        BiosVersionText.Text = $"BIOS Version: {systemInfo["BiosVersion"]}";
                    }

                    if (systemInfo.ContainsKey("CpuName") && CpuInfoText != null)
                    {
                        CpuInfoText.Text = $"CPU: {systemInfo["CpuName"]}";
                    }

                    if (systemInfo.ContainsKey("BaseBoardVersion") && BaseBoardVersionText != null)
                    {
                        BaseBoardVersionText.Text = $"BaseBoard Version: {systemInfo["BaseBoardVersion"]}";
                    }
                });

                WriteDebugToFile("📋 System information updated successfully");
            }
            catch (Exception ex)
            {
                WriteDebugToFile($"⚠️ Error updating system info: {ex.Message}");
            }
        }
    }
}
