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
        /// Creates a colored LED overlay by applying a color to the white pixels in a mask image
        /// </summary>
        /// <param name="maskPath">Path to the mask image (e.g., "ms-appx:///Assets/NucImages/nuc_front_mask.png")</param>
        /// <param name="ledColor">The color to apply to the white pixels</param>
        /// <returns>A WriteableBitmap with the colored LED overlay</returns>
        public static async Task<WriteableBitmap> CreateColoredLedOverlayAsync(string maskPath, Color ledColor)
        {
            try
            {
                // Load the mask image
                var uri = new Uri(maskPath);
                var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
                
                using var stream = await file.OpenAsync(FileAccessMode.Read);
                
                // Create decoder for the mask image
                var decoder = await BitmapDecoder.CreateAsync(stream);
                var pixelData = await decoder.GetPixelDataAsync();
                var pixels = pixelData.DetachPixelData();
                
                // Create a new WriteableBitmap
                var writeableBitmap = new WriteableBitmap((int)decoder.PixelWidth, (int)decoder.PixelHeight);
                
                // Process pixels: convert white pixels to the desired LED color
                var modifiedPixels = new byte[pixels.Length];
                
                for (int i = 0; i < pixels.Length; i += 4) // BGRA format
                {
                    byte b = pixels[i];     // Blue
                    byte g = pixels[i + 1]; // Green  
                    byte r = pixels[i + 2]; // Red
                    byte a = pixels[i + 3]; // Alpha
                    
                    // Check if pixel is white (or close to white)
                    if (r > 200 && g > 200 && b > 200 && a > 200)
                    {
                        // Replace with LED color
                        modifiedPixels[i] = ledColor.B;     // Blue
                        modifiedPixels[i + 1] = ledColor.G; // Green
                        modifiedPixels[i + 2] = ledColor.R; // Red
                        modifiedPixels[i + 3] = 255;        // Full alpha
                    }
                    else
                    {
                        // Keep transparent for non-white pixels
                        modifiedPixels[i] = 0;     // Blue
                        modifiedPixels[i + 1] = 0; // Green
                        modifiedPixels[i + 2] = 0; // Red
                        modifiedPixels[i + 3] = 0; // Transparent
                    }
                }
                
                // Write the modified pixels to the WriteableBitmap
                using var writeableStream = writeableBitmap.PixelBuffer.AsStream();
                await writeableStream.WriteAsync(modifiedPixels, 0, modifiedPixels.Length);
                writeableBitmap.Invalidate();
                
                return writeableBitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating colored LED overlay: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Helper method to convert from hex color string to Windows.UI.Color
        /// </summary>
        public static Color FromHex(string hex)
        {
            hex = hex.Replace("#", "");
            
            if (hex.Length == 6)
            {
                return Color.FromArgb(255, 
                    Convert.ToByte(hex.Substring(0, 2), 16),
                    Convert.ToByte(hex.Substring(2, 2), 16), 
                    Convert.ToByte(hex.Substring(4, 2), 16));
            }
            else if (hex.Length == 8)
            {
                return Color.FromArgb(
                    Convert.ToByte(hex.Substring(0, 2), 16),
                    Convert.ToByte(hex.Substring(2, 2), 16),
                    Convert.ToByte(hex.Substring(4, 2), 16),
                    Convert.ToByte(hex.Substring(6, 2), 16));
            }
            
            return Color.FromArgb(0, 0, 0, 0); // Transparent
        }
    }
}
