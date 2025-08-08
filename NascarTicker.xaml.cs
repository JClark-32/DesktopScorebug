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


namespace Desktop_Scorebug_WPF
{
    public partial class NascarTicker : Scoreboard
    {
        public NascarTicker()
        {
            InitializeComponent();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            base.OnContentRendered(e);
            getDriversArray();
        }

        private async void getDriversArray()
        {
            JObject feed = await getVehiclesArray(1);
            JArray drivers = getDrivers(feed);
            string flag_state = getFlagState(feed);

            Debug.WriteLine(drivers.ToString());
            Debug.WriteLine(flag_state);
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
                                await DownloadAndSaveImage(imgUrl, race, filename);
                            }
                        }
                    }
                }
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
