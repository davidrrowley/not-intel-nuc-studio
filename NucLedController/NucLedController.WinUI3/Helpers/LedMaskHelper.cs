using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace NucLedController.WinUI3.Helpers
{
    public static class LedMaskHelper
    {
        /// <summary>
        /// Tints a white-on-transparent (or light-on-dark) mask with a color, preserving soft edges.
        /// </summary>
        public static async Task<WriteableBitmap> CreateColoredLedOverlayAsync(string maskPath, Color ledColor, byte threshold = 16)
        {
            try
            {
                WriteDebugToFile($"🎨 Creating LED overlay from {maskPath} with color R:{ledColor.R} G:{ledColor.G} B:{ledColor.B}");
                
                // 1) Load - Handle both URI schemes and direct file paths
                IRandomAccessStream streamToUse = null;
                
                try
                {
                    // First try URI scheme (for packaged apps)
                    var uri = new Uri(maskPath);
                    var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
                    streamToUse = await file.OpenAsync(FileAccessMode.Read);
                    WriteDebugToFile($"✅ Loaded via URI scheme: {file.Name}");
                }
                catch (Exception uriEx)
                {
                    WriteDebugToFile($"❌ URI scheme failed: {uriEx.Message}");
                    WriteDebugToFile("🔄 Trying direct file path approach...");
                    
                    // Convert ms-appx:/// path to local file path for unpackaged apps
                    var localPath = maskPath.Replace("ms-appx:///", "").Replace("/", "\\");
                    WriteDebugToFile($"📝 Local path: {localPath}");
                    
                    // Try multiple possible base directories
                    string[] possibleBases = {
                        AppContext.BaseDirectory,
                        Directory.GetCurrentDirectory(),
                        Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                        Path.Combine(Directory.GetCurrentDirectory(), ".."),
                        Path.Combine(AppContext.BaseDirectory, "..")
                    };
                    
                    string foundPath = null;
                    foreach (var baseDir in possibleBases)
                    {
                        if (string.IsNullOrEmpty(baseDir)) continue;
                        
                        var testPath = Path.Combine(baseDir, localPath);
                        WriteDebugToFile($"📁 Checking: {testPath}");
                        
                        if (File.Exists(testPath))
                        {
                            foundPath = testPath;
                            WriteDebugToFile($"✅ Found file at: {foundPath}");
                            break;
                        }
                    }
                    
                    if (foundPath != null)
                    {
                        WriteDebugToFile($"🔄 Loading file via FileStream instead of StorageFile...");
                        
                        // Use FileStream instead of StorageFile for unpackaged apps
                        var fileStream = File.OpenRead(foundPath);
                        streamToUse = fileStream.AsRandomAccessStream();
                    }
                    else
                    {
                        WriteDebugToFile($"❌ File not found in any location");
                        WriteDebugToFile($"💡 AppContext.BaseDirectory: {AppContext.BaseDirectory}");
                        WriteDebugToFile($"💡 Current Directory: {Directory.GetCurrentDirectory()}");
                        throw new FileNotFoundException($"Mask file not found: {localPath}");
                    }
                }
                
                // Continue with processing using the stream (either from URI or FileStream)
                using (streamToUse)
                {
                    // 2) Decode to BGRA8 premultiplied explicitly
                    var decoder = await BitmapDecoder.CreateAsync(streamToUse);
                    WriteDebugToFile($"📐 Mask dimensions: {decoder.PixelWidth}x{decoder.PixelHeight}");
                    
                    var pixelProvider = await decoder.GetPixelDataAsync(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied,
                        new BitmapTransform(), // no scale/rotate
                        ExifOrientationMode.IgnoreExifOrientation,
                        ColorManagementMode.DoNotColorManage);

                    var src = pixelProvider.DetachPixelData(); // BGRA8 premultiplied
                    int w = (int)decoder.PixelWidth;
                    int h = (int)decoder.PixelHeight;

                    // 3) Prepare destination (also BGRA8 premultiplied)
                    var wb = new WriteableBitmap(w, h);
                    var dst = new byte[src.Length];

                    // Precompute color components (we'll premultiply per-pixel by alpha)
                    byte cr = ledColor.R;
                    byte cg = ledColor.G;
                    byte cb = ledColor.B;

                    int brightPixelCount = 0;
                    int processedPixelCount = 0;

                    // 4) Tint using mask luminance -> alpha (soft edges), with an optional floor threshold
                    for (int i = 0; i < src.Length; i += 4)
                    {
                        byte b = src[i + 0];
                        byte g = src[i + 1];
                        byte r = src[i + 2];
                        byte a = src[i + 3]; // already premultiplied in src

                        // Compute "whiteness" / luminance from RGB (ignore src premultipliedness for the mask;
                        // this is good enough because for masks we usually have a or rgb carrying the shape)
                        // Use Rec.601 luma: 0.299 R + 0.587 G + 0.114 B
                        int luma = (int)(0.299 * r + 0.587 * g + 0.114 * b);

                        // Combine with source alpha (so transparent areas stay transparent)
                        int maskAlpha = (luma * a) / 255;

                        // Count bright pixels for debugging
                        if (luma > threshold) brightPixelCount++;

                        // Optional threshold to suppress faint noise, but keep soft edges
                        if (maskAlpha < threshold) maskAlpha = 0;

                        // Premultiply the LED color by maskAlpha
                        byte outA = (byte)maskAlpha;
                        byte outR = (byte)((cr * maskAlpha) / 255);
                        byte outG = (byte)((cg * maskAlpha) / 255);
                        byte outB = (byte)((cb * maskAlpha) / 255);

                        dst[i + 0] = outB;
                        dst[i + 1] = outG;
                        dst[i + 2] = outR;
                        dst[i + 3] = outA;
                        
                        processedPixelCount++;
                    }

                    WriteDebugToFile($"🔍 Processed {processedPixelCount} pixels, found {brightPixelCount} bright pixels (threshold: {threshold})");

                    // 5) Write to the WriteableBitmap
                    using (var wbStream = wb.PixelBuffer.AsStream())
                    {
                        wbStream.Position = 0;
                        await wbStream.WriteAsync(dst, 0, dst.Length);
                    }
                    wb.Invalidate();

                    WriteDebugToFile("✅ LED overlay created successfully");
                    return wb;
                }
            }
            catch (Exception ex)
            {
                WriteDebugToFile($"❌ Error creating colored LED overlay: {ex}");
                return null;
            }
        }

        private static void WriteDebugToFile(string message)
        {
            try
            {
                var debugFile = Path.Combine(@"e:\Users\131858866\Github repos\not_intel_nuc_studio\NucLedController\logs", "NucLedDebug.txt");
                File.AppendAllText(debugFile, $"{DateTime.Now:HH:mm:ss.fff} - {message}\n");
            }
            catch { /* ignore file errors */ }
        }
        
        /// <summary>
        /// Helper method to convert from hex color string to Windows.UI.Color
        /// </summary>
        public static Color FromHex(string hex)
        {
            hex = hex.Replace("#", "");
            return hex.Length switch
            {
                6 => Color.FromArgb(255,
                    Convert.ToByte(hex.Substring(0, 2), 16),
                    Convert.ToByte(hex.Substring(2, 2), 16),
                    Convert.ToByte(hex.Substring(4, 2), 16)),
                8 => Color.FromArgb(
                    Convert.ToByte(hex.Substring(0, 2), 16),
                    Convert.ToByte(hex.Substring(2, 2), 16),
                    Convert.ToByte(hex.Substring(4, 2), 16),
                    Convert.ToByte(hex.Substring(6, 2), 16)),
                _ => Color.FromArgb(0, 0, 0, 0),
            };
        }
    }
}
