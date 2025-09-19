using HtmlAgilityPack;
using System.IO;
using System.Net.Http;
using System.Windows;
using Fastenshtein;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using System.Security.Policy;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Dynamic;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace Desktop_Scorebug_WPF
{
    public partial class NascarTicker : Scoreboard
    {
        private DispatcherTimer _timer;

        public NascarTicker()
        {
            InitializeComponent();
            InitializeTimer();
        }

        private void InitializeTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(10);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private async void Timer_Tick(object sender, EventArgs e)
        {
            string imagePath = "C:\\Users\\Jacob\\Desktop\\Desktop Scorebug WPF\\Desktop Scorebug WPF\\Images\\Temp\\#19.jpg";
            BitmapImage fillBitmap = new BitmapImage();

            updateFlagState();

            if (File.Exists(imagePath))
            {
                fillBitmap.BeginInit();
                fillBitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                fillBitmap.CacheOption = BitmapCacheOption.OnLoad;
                fillBitmap.EndInit();
                fillBitmap.Freeze();
            }
            else
            {
                fillBitmap = await getDefaultImageAsync(12);
            }

            FillImageWithImageMaskWidthBased(TickerP1Number, new Image { Source = fillBitmap });
        }

        private async Task<BitmapImage> getDefaultImageAsync(int number)
        {
            BitmapImage fillBitmap = new BitmapImage();

            using HttpClient httpClient = new();

            byte[] imageBytes = await httpClient.GetByteArrayAsync("https://cf.nascar.com/data/images/carbadges/1/" + 19 + ".png");

            using (var stream = new MemoryStream(imageBytes))
            {
                fillBitmap.BeginInit();
                fillBitmap.CacheOption = BitmapCacheOption.OnLoad;
                fillBitmap.StreamSource = stream;
                fillBitmap.EndInit();
                fillBitmap.Freeze(); // Optional but useful for threading
            }

            return fillBitmap;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            JArray drivers = [];

            getNumberCards("Darlington 2", 1);
            
            base.OnContentRendered(e);
            drivers = await getRaceStateArray();
        }

        private async Task<JArray> getRaceStateArray()
        {
            JObject feed = await getVehiclesArray(1);
            JArray drivers = getDrivers(feed);

            Debug.WriteLine(drivers.ToString());

            return drivers;
        }

        private async void updateFlagState()
        {
            JObject feed = await getVehiclesArray(1);
            changeFlagColor(feed);
        }


        private async Task<JObject> getVehiclesArray(int feed)
        {
            string url = "";
            if (feed == 1) url = "https://cf.nascar.com/live/feeds/live-feed.json";
            if (feed == 2) url = "https://cf.nascar.com/live/feeds/live-feed2.json";

            JObject json = await getJsonfromEndpoint(url);
            return json;
        }

        public static string CleanString(string dirtyString)
        {
            string noParentheses = Regex.Replace(dirtyString, @"\([^)]*\)", "");

            HashSet<char> removeChars = new HashSet<char>("?&^$#@!+-,:;<>’\'-_*");
            StringBuilder result = new StringBuilder(noParentheses.Length);
            foreach (char c in noParentheses)
                if (!removeChars.Contains(c))
                    result.Append(c);

            return result.ToString().Trim();
        }

        private JArray getDrivers(JObject json)
        {
            JArray array = (JArray)json["vehicles"];

            JArray names = [];

            foreach (JObject eventObj in array)
            {
                // This grabs the "name" property if it exists directly in the event object
                var driverToken = eventObj["driver"];
                if (driverToken == null) continue;
                
                var driverName = driverToken["last_name"];
                if (driverToken != null)
                {
                    string name = driverName.ToString();
                    names.Add(CleanString(name));
                }
            }
            //Debug.WriteLine(names.ToString());
            return names;
        }

        private async Task<JArray> getNumbersArray()
        {
            JObject feed = await getVehiclesArray(1);

            JArray array = (JArray)feed["vehicles"];

            JArray numbers = [];

            foreach (JObject eventObj in array)
            {
                // This grabs the "name" property if it exists directly in the event object
                var driverNumber = eventObj["vehicle_number"];
                if (driverNumber == null) continue;

                numbers.Add(driverNumber);
            }
            //Debug.WriteLine(names.ToString());
            return numbers;
        }

        private string getDriverAtPosition(JArray Drivers, int position)
        {
            return Drivers[position - 1].ToString();
        }

        private string getNumber(JArray Numbers, int position)
        {
            return Numbers[position-1].ToString();
        }

        private string getFlagState(JObject json)
        {
            string state = "";

            var flag_state = json["flag_state"];

            if (flag_state != null)
                state = flag_state.ToString();

            return state;
        }

        private async void getNumberCards(string race, int series_id)
        {
            string division = "";
            switch (series_id)
            {
                case 1:
                    division = "cup";
                    break;
                case 2:
                    division = "xfinity";
                    break;
                case 3:
                    division = "truck";
                    break;
                default:
                    division = "";
                    break;
            }

            var url = $"https://diecastcharv.com/{division}-number-card-tracker/";
            var htmlContent = await GetPageContent(url);

            if (!string.IsNullOrEmpty(htmlContent))
            {
                var result = ExtractDriverData(htmlContent, race);
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
                                await DownloadAndSaveImage(imgUrl, "C:\\Users\\Jacob\\Desktop\\Desktop Scorebug WPF\\Desktop Scorebug WPF\\Images\\Temp\\", filename);
                            }
                        }
                    }
                }
            }
        }

        private void changeFlagColor(JObject feed)
        {
            string state = getFlagState(feed);

            /*
             * 1: Green Flag
             * 2: Yellow Flag
             * 3: Red Flag -- Unconfirmed
             * 19: Checkard Flag
             */

            switch(state){
                case "1":
                    RecolorImageWithAlpha(TickerFlag, System.Drawing.ColorTranslator.FromHtml("#00b509"));
                    break;
                case "2":
                    RecolorImageWithAlpha(TickerFlag, System.Drawing.ColorTranslator.FromHtml("#ffea00"));
                    break;
                default:
                    //TODO: ADD REMOVE RECOLOR FUNCTION TO REVERT BACK TO CHECKARD FLAG TEXTURE
                    break;

            }
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
                // Convert relative path to absolute path
                var fullDirectory = System.IO.Path.GetFullPath(directory);

                if (!Directory.Exists(fullDirectory))
                    Directory.CreateDirectory(fullDirectory);

                using var client = new HttpClient();
                var data = await client.GetAsync(url);
                data.EnsureSuccessStatusCode();

                var path = System.IO.Path.Combine(fullDirectory, filename);
                await using var stream = await data.Content.ReadAsStreamAsync();
                await using var fs = new FileStream(path, FileMode.Create);
                await stream.CopyToAsync(fs);
            }
            catch (Exception e)
            {
                // Handle error as needed
                // e.g., log it or display it
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

        private async void P1NameLoaded(object sender, RoutedEventArgs e)
        {
            string p1Name = "";

            JObject feed = await getVehiclesArray(1);
            JArray drivers = getDrivers(feed);

            p1Name = getDriverAtPosition(drivers, 1);

            ReplaceSquareInImageWithTextBox(TickerP1Name, "P1Name");

            TextBox found = (TextBox)Podium.Children
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Name == "P1Name");
            if (found != null)

            {
                found.Text = p1Name;
                AddTextOutline(found, Colors.Black, 10.0);
            }
        }
        private async void P1NumberLoaded(object sender, RoutedEventArgs e)
        {
            string number = getNumber(await getNumbersArray(), 1);

            string imagePath = "C:\\Users\\Jacob\\Desktop\\Desktop Scorebug WPF\\Desktop Scorebug WPF\\Images\\Temp\\#" + number +".jpg";
            BitmapImage fillBitmap = new BitmapImage();

            if (File.Exists(imagePath))
            {
                fillBitmap.BeginInit();
                fillBitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                fillBitmap.CacheOption = BitmapCacheOption.OnLoad;
                fillBitmap.EndInit();
                fillBitmap.Freeze();

                FillImageWithImageMaskWidthBased(TickerP1Number, new Image { Source = fillBitmap });
            }
        }

        private async void P1TimingLoaded(object sender, RoutedEventArgs e)
        {
            ReplaceSquareInImageWithTextBox(TickerP1Timing, "P1Timing");

            TextBox found = (TextBox)Podium.Children
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Name == "P1Timing");
            if (found != null)

            {
                found.Text = "";
                AddTextOutline(found, Colors.Black, 10.0);
            }
        }
        protected override void OnClosed(EventArgs e)
        {
            string folderPath = "C:\\Users\\Jacob\\Desktop\\Desktop Scorebug WPF\\Desktop Scorebug WPF\\Images\\Temp";

            if (Directory.Exists(folderPath))
            {
                // Delete all files
                foreach (string file in Directory.GetFiles(folderPath))
                {
                    File.Delete(file);
                }

                // Delete all subdirectories (and their contents)
                foreach (string dir in Directory.GetDirectories(folderPath))
                {
                    Directory.Delete(dir, true); // true = recursive
                }

                Console.WriteLine("Folder contents wiped successfully.");
            }
            else
            {
                Console.WriteLine("Folder not found.");
            }
            base.OnClosed(e);
        }
    }
}
