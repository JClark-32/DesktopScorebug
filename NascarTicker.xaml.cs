using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Fastenshtein;
using System.Diagnostics;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Media.Animation;
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
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Dynamic;
using System.Text;
using System.Windows.Threading;
using System.Xml.Serialization;

namespace Desktop_Scorebug_WPF
{
    public partial class NascarTicker : Window
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



        private string division = "cup";

        public NascarTicker()
        {
            InitializeComponent();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CenterTopOnScreen();
            MakeWindowClickThrough();
            TrackMouseAsync(_cts.Token);
        }
        
        /*
        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            DriverList.Items.Clear();
            StatusText.Text = "Fetching data...";

            var targetRace = RaceInput.Text.Trim();
            Debug.WriteLine(targetRace);

            if (string.IsNullOrWhiteSpace(targetRace))
            {
                StatusText.Text = "Please enter a race name.";
                return;
            }

            var url = $"https://diecastcharv.com/{division}-number-card-tracker/";
            var htmlContent = await GetPageContent(url);

            if (!string.IsNullOrEmpty(htmlContent))
            {
                var result = ExtractDriverData(htmlContent, targetRace);
                if (result.Any())
                {
                    foreach (var driver in result)
                    {
                        var driverName = driver["Name"].Replace("joy", "joey").Replace("a.j.", "a-j");
                        var scheme = driver["Races"][0]["Scheme"];
                        var images = await GetSponsorImages(driverName, division, scheme);

                        if (images.Count > 0)
                        {
                            foreach (var imgUrl in images)
                            {
                                string filename = $"{driver["Number"]}.jpg";
                                await DownloadAndSaveImage(imgUrl, targetRace, filename);
                                DriverList.Items.Add($"{driver["Number"]} - {driverName} - Image saved");
                            }
                        }
                        else
                        {
                            DriverList.Items.Add($"{driver["Number"]} - {driverName} - No image found");
                        }
                    }
                    StatusText.Text = "Done.";
                }
                else
                {
                    StatusText.Text = "No results found for that race.";
                }
            }
            else
            {
                StatusText.Text = "Failed to load page.";
            }
        }
        */

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

        async Task<string> GetPageContent(string url)
        {
            try
            {
                using var client = new HttpClient();
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception e)
            {
                //StatusText.Text = $"Request error: {e.Message}";
                return null;
            }
        }

        List<Dictionary<string, dynamic>> ExtractDriverData(string html, string targetRace)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var driverList = new List<Dictionary<string, dynamic>>();
            var groups = doc.DocumentNode.SelectNodes("//div[contains(@class, 'wp-block-group')]");

            if (groups == null) return driverList;

            foreach (var div in groups)
            {
                try
                {
                    var p = div.SelectNodes("p");
                    if (p == null || p.Count < 2) continue;

                    var number = p[0].InnerText.Trim();
                    var name = p[1].InnerText.Trim();

                    if (!number.StartsWith("#")) continue;

                    var races = new List<Dictionary<string, string>>();
                    var table = div.SelectSingleNode("following-sibling::figure[1][contains(@class, 'wp-block-table')]");
                    if (table != null)
                    {
                        var rows = table.SelectNodes(".//tbody//tr");
                        if (rows != null)
                        {
                            foreach (var row in rows)
                            {
                                var cols = row.SelectNodes("td");
                                if (cols == null || cols.Count < 2) continue;

                                var scheme = cols[0].InnerText.Trim();
                                var raceData = string.Join(", ", cols[1].InnerText
                                    .Split(new[] { '\n', '<', '>' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(s => s.Trim()));

                                if (raceData.ToLower().Contains(targetRace.ToLower()))
                                {
                                    races.Add(new Dictionary<string, string>
                            {
                                { "Scheme", scheme },
                                { "Races", raceData }
                            });
                                }
                            }
                        }
                    }

                    if (races.Any())
                    {
                        driverList.Add(new Dictionary<string, dynamic>
                {
                    { "Number", number },
                    { "Name", name.Replace(" ", "-").ToLower() },
                    { "Races", races }
                });
                    }
                }
                catch { continue; }
            }

            return driverList;
        }

        /*
        private void RaceInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            Placeholder.Visibility = string.IsNullOrEmpty(RaceInput.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void RaceInput_GotFocus(object sender, RoutedEventArgs e)
        {
            Placeholder.Visibility = Visibility.Collapsed;
        }

        private void RaceInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(RaceInput.Text))
                Placeholder.Visibility = Visibility.Visible;
        }
        */


        async Task<List<string>> GetSponsorImages(string driverName, string division, string specificSponsor = null)
        {
            try
            {
                driverName = driverName.Replace(" ", "-").ToLower();
                var url = driverName != "rajah-caruth"
                    ? $"https://diecastcharv.com/2025-{driverName}-{division}-number-cards/"
                    : $"https://diecastcharv.com/2025-{driverName}-{division}-number-cads/";

                using var client = new HttpClient();
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var doc = new HtmlDocument();
                doc.LoadHtml(await response.Content.ReadAsStringAsync());

                var sponsorImages = new Dictionary<string, string>();
                var sponsors = doc.DocumentNode.SelectNodes("//p[contains(@class, 'has-text-align-center')]");

                if (sponsors != null)
                {
                    foreach (var sponsor in sponsors)
                    {
                        try
                        {
                            var name = sponsor.SelectSingleNode("strong").InnerText.Trim();
                            if (name.Contains("NUMBER CARDS ARE ORGANIZED")) continue;

                            var figure = sponsor.SelectSingleNode("following-sibling::figure");
                            if (figure != null)
                            {
                                var imgs = figure.SelectNodes(".//figure[contains(@class, 'wp-block-image')]");
                                if (imgs != null && imgs.Count > 1)
                                {
                                    var img = imgs[1].SelectSingleNode(".//img");
                                    sponsorImages[name.Split(':')[0]] = img?.GetAttributeValue("src", "none");
                                }
                            }
                        }
                        catch { continue; }
                    }
                }

                if (!string.IsNullOrEmpty(specificSponsor))
                {
                    var match = BestMatch(specificSponsor, sponsorImages.Keys);
                    return new List<string> { sponsorImages[match] };
                }

                return sponsorImages.Values.ToList();
            }
            catch (Exception e)
            {
                //StatusText.Text = $"Error getting images: {e.Message}";
                return new List<string>();
            }
        }

        async Task DownloadAndSaveImage(string url, string directory, string filename)
        {
            try
            {
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                using var client = new HttpClient();
                var data = await client.GetAsync(url);
                data.EnsureSuccessStatusCode();

                var path = Path.Combine(directory, filename);
                await using var stream = await data.Content.ReadAsStreamAsync();
                await using var fs = new FileStream(path, FileMode.Create);
                await stream.CopyToAsync(fs);
            }
            catch (Exception e)
            {
                //StatusText.Text = $"Image error: {e.Message}";
            }
        }

        string BestMatch(string sponsor, IEnumerable<string> sponsorList)
        {
            var bestScore = 0.0;
            var best = sponsorList.First();
            foreach (var s in sponsorList)
            {
                var score = GetSimilarity(sponsor.ToLower(), s.ToLower());
                if (score > bestScore)
                {
                    bestScore = score;
                    best = s;
                }
            }
            return best;
        }

        double GetSimilarity(string a, string b)
        {
            var matcher = new Levenshtein(a);
            var dist = matcher.DistanceFrom(b);
            return 1.0 - (double)dist / Math.Max(a.Length, b.Length);
        }
    }
}
