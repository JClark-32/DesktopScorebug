using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Media;
using System.Drawing;
using System.Net.Http;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Security.AccessControl;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Newtonsoft.Json.Linq;
using String = System.String;
using System.Diagnostics;

namespace Desktop_Scorebug_WPF
{
    public partial class MainWindow : Window
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

        private String team1LogoUrl = "https://a.espncdn.com/i/teamlogos/nfl/500/kc.png";
        private String team1Color = "#e31837";

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += Window_Loaded; // wire Loaded in code
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CenterTopOnScreen();
            MakeWindowClickThrough();
            TrackMouseAsync(_cts.Token);
            getJson();
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

        private POINT GetMouseScreenPosition()
        {
            GetCursorPos(out POINT point);
            return point;
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

        protected override void OnClosed(EventArgs e)
        {
            _cts.Cancel();
            base.OnClosed(e);
        }

        private async void getJson()
        {
            string url = "https://cdn.espn.com/core/nfl/scoreboard?xhr=1&limit=50";
            using HttpClient client = new HttpClient();
            try
            {
                string json = await client.GetStringAsync(url);
                JObject joResponse = JObject.Parse(json);
                JObject ojObject = (JObject)joResponse["content"];
                JObject ojObject1 = (JObject)ojObject["sbData"];

                JArray array = (JArray)ojObject1["events"];
                //JArray array1 = (JArray)array["competitions"];
                //Debug.WriteLine(array1.ToString());

                var nameValues = array.SelectTokens("$..name");
                foreach (JObject eventObj in array)
                {
                    // This grabs the "name" property if it exists directly in the event object
                    JToken nameToken = eventObj["name"];
                    if (nameToken != null)
                    {
                        Debug.WriteLine(nameToken.ToString());
                    }
                }

                //Debug.WriteLine(array.ToString());
            }
            catch
            {
                Console.WriteLine($"Request error");
            }


            
        }

        //Start color changing group 
        private void RecolorImageWithAlpha(Image targetImage, System.Drawing.Color targetColor)
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

        private void FillImageWithImageMask(Image targetImage, Image fillImage)
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


        private void BGColorLoaded(object sender, RoutedEventArgs e)
        {
            var TeamColor1 = System.Drawing.ColorTranslator.FromHtml(team1Color);
            RecolorImageWithAlpha(BackGroundTeamColors, TeamColor1);
        }
        

        private async void TeamLogoLoaded(object sender, RoutedEventArgs e)
        {
            using HttpClient httpClient = new();
            byte[] imageBytes = await httpClient.GetByteArrayAsync(team1LogoUrl);

            BitmapImage fillBitmap = new BitmapImage();

            using (var stream = new MemoryStream(imageBytes))
            {
                fillBitmap.BeginInit();
                fillBitmap.CacheOption = BitmapCacheOption.OnLoad;
                fillBitmap.StreamSource = stream;
                fillBitmap.EndInit();
                fillBitmap.Freeze(); // Optional but useful for threading
            }

            FillImageWithImageMask(TeamLogo1, new Image { Source = fillBitmap });
        }

        //end color change group
    }
}
