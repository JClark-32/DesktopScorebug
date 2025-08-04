using HtmlAgilityPack;
using System.IO;
using System.Net.Http;
using System.Windows;
using Fastenshtein;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Media.Animation;
using System.Windows.Threading;


namespace Desktop_Scorebug_WPF
{
    public partial class NascarTicker : Scoreboard
    {
        private string division = "cup";

        public NascarTicker()
        {
            InitializeComponent();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            base.OnContentRendered(e);
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
