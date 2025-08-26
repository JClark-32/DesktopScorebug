using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;

namespace Desktop_Scorebug_WPF
{
    public partial class Scoreboard : Window
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;

        private CancellationTokenSource _cts = new();
        private bool isFadingOut = false;
        private bool isFadingIn = false;

        // P/Invoke declarations
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLong(IntPtr hwnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLong(IntPtr hwnd, int nIndex);

        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        const uint MONITOR_DEFAULTTONEAREST = 2;

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            CenterTopOnScreen();
            MakeWindowClickThrough();
            TrackMouseAsync(_cts.Token);
        }

        private void CenterTopOnScreen()
        {
            var screenWidth = SystemParameters.WorkArea.Width;
            this.Left = (screenWidth - this.Width) / 2;
            this.Top = 0;
        }

        private void MakeWindowClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                new IntPtr(extendedStyle.ToInt32() | WS_EX_LAYERED | WS_EX_TRANSPARENT));
        }

        private async void TrackMouseAsync(CancellationToken token)
        {
            RECT workArea = GetCurrentMonitorWorkArea();

            while (!token.IsCancellationRequested)
            {
                var pos = GetMouseScreenPosition();

                // Check if mouse Y is in top 1/8th of the monitor's work area
                if (pos.Y >= workArea.Top && pos.Y < workArea.Top + (workArea.Bottom - workArea.Top) / 8
                    && pos.X >= workArea.Left && pos.X < workArea.Right)
                {
                    Dispatcher.Invoke(() => FadeOut());
                }
                else
                {
                    Dispatcher.Invoke(() => FadeIn());
                }

                await Task.Delay(50);
            }
        }

        private RECT GetCurrentMonitorWorkArea()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            MONITORINFO monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            GetMonitorInfo(hMonitor, ref monitorInfo);

            return monitorInfo.rcWork;  // This RECT represents the monitor's working area
        }

        private POINT GetMouseScreenPosition()
        {
            GetCursorPos(out POINT point);
            return point;
        }

        private void FadeOut()
        {
            if (isFadingOut) return;
            isFadingOut = true;
            isFadingIn = false;

            var animation = new DoubleAnimation
            {
                To = 0.1,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            this.BeginAnimation(Window.OpacityProperty, animation);
        }

        private void FadeIn()
        {
            if (isFadingIn) return;
            isFadingIn = true;
            isFadingOut = false;

            var animation = new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            this.BeginAnimation(Window.OpacityProperty, animation);
        }

        public void ReplaceSquareInImageWithTextBox(Image imageControl, string textBoxName)
        {
            if (imageControl.Source == null)
                throw new InvalidOperationException("Image control has no source.");

            // Convert to BitmapSource (if not already)
            BitmapSource source = ConvertToBitmapSource(imageControl.Source);

            // Convert to WriteableBitmap
            WriteableBitmap writable = new WriteableBitmap(source);
            int width = writable.PixelWidth;
            int height = writable.PixelHeight;

            int[] pixels = new int[width * height];
            writable.CopyPixels(pixels, width * 4, 0);

            // Find non-transparent square bounds
            int minX = width, minY = height, maxX = 0, maxY = 0;
            bool found = false;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int pixel = pixels[y * width + x];
                    byte alpha = (byte)((pixel >> 24) & 0xFF);
                    if (alpha > 0)
                    {
                        found = true;
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (!found)
                return;

            // Make detected square transparent
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    pixels[y * width + x] = 0x00000000;
                }
            }

            WriteableBitmap updated = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            updated.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            imageControl.Source = updated;

            // Scale from image space to control space
            double scaleX = imageControl.ActualWidth / width;
            double scaleY = imageControl.ActualHeight / height;

            Rect rect = new Rect(
                minX * scaleX,
                minY * scaleY,
                (maxX - minX + 1) * scaleX,
                (maxY - minY + 1) * scaleY
            );

            // Create and position TextBox
            TextBox textBox = new TextBox
            {
                Name = textBoxName,
                Width = rect.Width,
                Height = rect.Height,
                FontSize = rect.Height * 0.9,
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Font/#Bebas Neue"),
                Text = "-"
            };

            // Add to panel and position with Margin
            if (imageControl.Parent is Panel panel)
            {
                panel.Children.Add(textBox);

                // Translate coordinates from image to parent panel
                System.Windows.Point topLeft = imageControl.TranslatePoint(new System.Windows.Point(rect.X, rect.Y), panel);

                textBox.Margin = new Thickness(topLeft.X, topLeft.Y, 0, 0);
                textBox.HorizontalAlignment = HorizontalAlignment.Left;
                textBox.VerticalAlignment = VerticalAlignment.Top;
            }
            else
            {
                throw new InvalidOperationException("Image's parent must be a Panel (Grid, StackPanel, etc.)");
            }
        }

        public static async Task<JObject> getJsonfromEndpoint(string url)
        {
            using HttpClient client = new HttpClient();
            try
            {
                string json = await client.GetStringAsync(url);
                JObject joResponse = JObject.Parse(json);
                return joResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Request error: {ex.Message}");
            }
            return null;
        }

        private BitmapSource ConvertToBitmapSource(ImageSource source)
        {
            if (source is BitmapSource bitmapSource)
            {
                return bitmapSource;
            }

            throw new InvalidOperationException("Unsupported image source type.");
        }

        public void AddTextOutline(TextBox textBox, System.Windows.Media.Color outlineColor, double thickness = 1.5)
        {
            textBox.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = outlineColor,
                Direction = 0,
                ShadowDepth = 0,
                Opacity = 1,
                BlurRadius = thickness
            };
        }

        public void RecolorImageWithAlpha(Image targetImage, System.Drawing.Color targetColor)
        {
            if (targetImage.Source is not BitmapSource sourceBitmap)
                throw new InvalidOperationException("The provided Image does not contain a valid BitmapSource.");

            // Ensure it's in BGRA32 format (32bpp with alpha)
            FormatConvertedBitmap formattedBitmap = new FormatConvertedBitmap();
            formattedBitmap.BeginInit();
            formattedBitmap.Source = sourceBitmap;
            formattedBitmap.DestinationFormat = PixelFormats.Bgra32;
            formattedBitmap.EndInit();

            int width = formattedBitmap.PixelWidth;
            int height = formattedBitmap.PixelHeight;
            int stride = width * 4;
            byte[] pixelData = new byte[height * stride];

            formattedBitmap.CopyPixels(pixelData, stride, 0);

            for (int i = 0; i < pixelData.Length; i += 4)
            {
                byte alpha = pixelData[i + 3]; // Alpha stays

                pixelData[i] = targetColor.B; // Blue
                pixelData[i + 1] = targetColor.G; // Green
                pixelData[i + 2] = targetColor.R; // Red
                pixelData[i + 3] = alpha;
            }

            WriteableBitmap recoloredBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            recoloredBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixelData, stride, 0);

            targetImage.Source = recoloredBitmap;
        }

        public void FillImageWithImageMaskWidthBased(Image targetImage, Image fillImage)
        {
            if (targetImage.Source is not BitmapSource shapeBitmap)
                throw new InvalidOperationException("The target Image does not contain a valid BitmapSource.");
            if (fillImage.Source is not BitmapSource fillBitmap)
                throw new InvalidOperationException("The fill Image does not contain a valid BitmapSource.");

            int shapeWidth = shapeBitmap.PixelWidth;
            int shapeHeight = shapeBitmap.PixelHeight;
            int stride = shapeWidth * 4;

            // Convert shape to BGRA32
            var shapeFormatted = new FormatConvertedBitmap(shapeBitmap, PixelFormats.Bgra32, null, 0);
            byte[] shapePixels = new byte[shapeHeight * stride];
            shapeFormatted.CopyPixels(shapePixels, stride, 0);

            // Find bounding box of visible (non-transparent) pixels
            int minX = shapeWidth, minY = shapeHeight, maxX = 0, maxY = 0;
            for (int y = 0; y < shapeHeight; y++)
            {
                for (int x = 0; x < shapeWidth; x++)
                {
                    int i = (y * shapeWidth + x) * 4;
                    byte alpha = shapePixels[i + 3];
                    if (alpha > 0)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (minX >= maxX || minY >= maxY)
                return;

            int boxWidth = maxX - minX + 1;
            int boxHeight = maxY - minY + 1;

            // Scale fill image proportionally based only on width
            double scale = (double)boxWidth / fillBitmap.PixelWidth;
            int scaledWidth = (int)(fillBitmap.PixelWidth * scale);
            int scaledHeight = (int)(fillBitmap.PixelHeight * scale);

            int offsetX = minX + (boxWidth - scaledWidth) / 2;
            int offsetY = minY + (boxHeight - scaledHeight) / 2;

            // Scale the fill image
            var scaledFill = new TransformedBitmap(fillBitmap, new ScaleTransform(scale, scale));
            var fillFormatted = new FormatConvertedBitmap(scaledFill, PixelFormats.Bgra32, null, 0);

            byte[] fillPixels = new byte[scaledHeight * scaledWidth * 4];
            int fillStride = scaledWidth * 4;
            fillFormatted.CopyPixels(fillPixels, fillStride, 0);

            // Prepare transparent output buffer
            byte[] finalPixels = new byte[shapeHeight * stride];

            // Blend fill image with shape alpha
            for (int y = 0; y < scaledHeight; y++)
            {
                for (int x = 0; x < scaledWidth; x++)
                {
                    int destX = offsetX + x;
                    int destY = offsetY + y;

                    if (destX < 0 || destX >= shapeWidth || destY < 0 || destY >= shapeHeight)
                        continue;

                    int srcIndex = (y * scaledWidth + x) * 4;
                    int dstIndex = (destY * shapeWidth + destX) * 4;

                    byte shapeAlpha = shapePixels[dstIndex + 3];
                    byte fillAlpha = fillPixels[srcIndex + 3];

                    if (shapeAlpha == 0) continue; // keep transparent

                    // Combine alpha (multiply mask alpha with fill alpha)
                    double alphaFactor = shapeAlpha / 255.0 * fillAlpha / 255.0;

                    finalPixels[dstIndex + 0] = (byte)(fillPixels[srcIndex + 0] * alphaFactor);
                    finalPixels[dstIndex + 1] = (byte)(fillPixels[srcIndex + 1] * alphaFactor);
                    finalPixels[dstIndex + 2] = (byte)(fillPixels[srcIndex + 2] * alphaFactor);
                    finalPixels[dstIndex + 3] = (byte)(255 * alphaFactor);
                }
            }

            WriteableBitmap result = new WriteableBitmap(shapeWidth, shapeHeight, 96, 96, PixelFormats.Bgra32, null);
            result.WritePixels(new Int32Rect(0, 0, shapeWidth, shapeHeight), finalPixels, stride, 0);

            targetImage.Source = result;
        }


        public void FillImageWithImageMask(Image targetImage, Image fillImage)
        {
            if (targetImage.Source is not BitmapSource shapeBitmap)
                throw new InvalidOperationException("The target Image does not contain a valid BitmapSource.");
            if (fillImage.Source is not BitmapSource fillBitmap)
                throw new InvalidOperationException("The fill Image does not contain a valid BitmapSource.");

            int shapeWidth = shapeBitmap.PixelWidth;
            int shapeHeight = shapeBitmap.PixelHeight;
            int stride = shapeWidth * 4;

            // Convert shape to BGRA32
            var shapeFormatted = new FormatConvertedBitmap(shapeBitmap, PixelFormats.Bgra32, null, 0);
            byte[] shapePixels = new byte[shapeHeight * stride];
            shapeFormatted.CopyPixels(shapePixels, stride, 0);

            // Find bounding box of visible (non-transparent) pixels
            int minX = shapeWidth, minY = shapeHeight, maxX = 0, maxY = 0;
            for (int y = 0; y < shapeHeight; y++)
            {
                for (int x = 0; x < shapeWidth; x++)
                {
                    int i = (y * shapeWidth + x) * 4;
                    byte alpha = shapePixels[i + 3];
                    if (alpha > 0)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (minX >= maxX || minY >= maxY)
                return;

            int boxWidth = maxX - minX + 1;
            int boxHeight = maxY - minY + 1;

            // Scale fill image proportionally to fit inside bounding box
            double scaleX = (double)boxWidth / fillBitmap.PixelWidth;
            double scaleY = (double)boxHeight / fillBitmap.PixelHeight;
            double scale = Math.Min(scaleX, scaleY);

            int scaledWidth = (int)(fillBitmap.PixelWidth * scale);
            int scaledHeight = (int)(fillBitmap.PixelHeight * scale);

            int offsetX = minX + (boxWidth - scaledWidth) / 2;
            int offsetY = minY + (boxHeight - scaledHeight) / 2;

            // Scale the fill image
            var scaledFill = new TransformedBitmap(fillBitmap, new ScaleTransform(scale, scale));
            var fillFormatted = new FormatConvertedBitmap(scaledFill, PixelFormats.Bgra32, null, 0);

            byte[] fillPixels = new byte[scaledHeight * scaledWidth * 4];
            int fillStride = scaledWidth * 4;
            fillFormatted.CopyPixels(fillPixels, fillStride, 0);

            // Prepare transparent output buffer
            byte[] finalPixels = new byte[shapeHeight * stride];

            // Paste the fill image into the center of the shape bounds
            for (int y = 0; y < scaledHeight; y++)
            {
                for (int x = 0; x < scaledWidth; x++)
                {
                    int destX = offsetX + x;
                    int destY = offsetY + y;

                    if (destX < 0 || destX >= shapeWidth || destY < 0 || destY >= shapeHeight)
                        continue;

                    int srcIndex = (y * scaledWidth + x) * 4;
                    int dstIndex = (destY * shapeWidth + destX) * 4;

                    // Copy fill image pixels, including original alpha
                    finalPixels[dstIndex + 0] = fillPixels[srcIndex + 0]; // B
                    finalPixels[dstIndex + 1] = fillPixels[srcIndex + 1]; // G
                    finalPixels[dstIndex + 2] = fillPixels[srcIndex + 2]; // R
                    finalPixels[dstIndex + 3] = fillPixels[srcIndex + 3]; // A (preserved!)
                }
            }

            WriteableBitmap result = new WriteableBitmap(shapeWidth, shapeHeight, 96, 96, PixelFormats.Bgra32, null);
            result.WritePixels(new Int32Rect(0, 0, shapeWidth, shapeHeight), finalPixels, stride, 0);

            targetImage.Source = result;
        }
    }
}
