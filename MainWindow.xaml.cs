using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System;


namespace Desktop_Scorebug_WPF
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLong(IntPtr hwnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLong(IntPtr hwnd, int nIndex);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;

        private CancellationTokenSource _cts = new();
        private bool isFadingOut = false;
        private bool isFadingIn = false;

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
            
            
        }

        private void BGColorLoaded(object sender, RoutedEventArgs e)
        {
            var TeamColor1 = System.Drawing.Color.FromArgb(0, 44, 95);
            RecolorImageWithAlpha(BackGroundTeamColors, TeamColor1);
        }
        private void ScoreBarLoaded(object sender, RoutedEventArgs e)
        {
            var TeamColor1 = System.Drawing.Color.FromArgb(0, 37, 81);
            RecolorImageWithAlpha(BackGroundTeamScoreBar1, TeamColor1);
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
            var screenHeight = SystemParameters.VirtualScreenHeight;
            var screenWidth = SystemParameters.VirtualScreenWidth;

            while (!token.IsCancellationRequested)
            {
                var position = GetMouseScreenPosition();

                if (position.Y < screenHeight / 8 && position.X < screenWidth)
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
    }
}
