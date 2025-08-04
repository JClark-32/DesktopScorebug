using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Media;
using System.Net.Http;
using System.IO;
using Newtonsoft.Json.Linq;
using String = System.String;
using System.Diagnostics;
using System.Windows.Threading;

namespace Desktop_Scorebug_WPF
{
    public partial class FootballScoreboard : Scoreboard
    {

        string urlDate;
        string gameName;
        string league;

        private DispatcherTimer _timer;
        public FootballScoreboard(string league, string gameName, string urlDate)
        {
            this.urlDate = urlDate;
            this.gameName = gameName;
            this.league = league;
            InitializeComponent();
            InitializeTimer();
            this.Loaded += Window_Loaded;
        }

        private void InitializeTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(5);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            updateScoreboard();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            base.OnContentRendered(e);
            //Debug.WriteLine(DateTime.Now.ToString("H,m"));
        }

        protected override void OnClosed(EventArgs e)
        {
            //_cts.Cancel();
            base.OnClosed(e);
        }

        public class TeamInfo
        {
            public string TeamName { get; set; }
            public string Color { get; set; }
            public string Logo { get; set; }
        }

        public List<TeamInfo> GetTeamsByMatchupName(JArray eventsArray, string matchupName)
        {
            var teams = new List<TeamInfo>();

            foreach (JObject eventObj in eventsArray)
            {
                string name = eventObj["name"]?.ToString();
                if (name != matchupName)
                    continue;

                var competitors = eventObj["competitions"]?[0]?["competitors"] as JArray;
                if (competitors == null) continue;

                foreach (var competitor in competitors)
                {
                    var team = competitor["team"];
                    if (team == null) continue;

                    string teamName = team["displayName"]?.ToString();
                    string color = team["color"]?.ToString();
                    string logo = team["logo"]?.ToString();

                    teams.Add(new TeamInfo
                    {
                        TeamName = teamName,
                        Color = color,
                        Logo = logo
                    });
                }

                break; // we found the matchup, no need to continue
            }

            return teams;
        }

        private static async Task<JObject> getJsonfromEndpoint(String urlDate, String league)
        {
            string url = "https://site.api.espn.com/apis/site/v2/sports/football/"+league+"/scoreboard?dates=" + urlDate;
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

        private async Task<JArray> getEvents(String urlDate, String league)
        {
            JObject json = await getJsonfromEndpoint(urlDate, league);
            JArray array = (JArray)json["events"];
            if(array == null) 
                return [];
            return array;
        }

        private string getTeamColor(string Matchup, int Team, JArray eventsArray)
        {
            string defaultColor = "#FFFFFF"; // fallback color
            string color = defaultColor;

            foreach (JObject eventObj in eventsArray)
            {
                string name = eventObj["name"]?.ToString();
                if (name != Matchup)
                    continue;

                var competitors = eventObj["competitions"]?[0]?["competitors"] as JArray;
                if (competitors == null) continue;

                var team = competitors[Team]?["team"];
                if (team == null) continue;

                if (league.Equals("nfl"))
                {
                    string? baseColor = team["color"]?.ToString();
                    color = !string.IsNullOrWhiteSpace(baseColor) ? $"#{baseColor}" : defaultColor;
                }
                else
                {
                    string? altColor = team["alternateColor"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(altColor))
                    {
                        color = $"#{altColor}";
                    }
                    else
                    {
                        string? baseColor = team["color"]?.ToString();
                        color = !string.IsNullOrWhiteSpace(baseColor) ? $"#{baseColor}" : defaultColor;
                    }
                }

                break;
            }

            return color;
        }


        private string getAbbreviation(String Matchup, int Team, JArray eventsArray)
        {
            string abbreviation = "";

            foreach (JObject eventObj in eventsArray)
            {
                string name = eventObj["name"]?.ToString();
                if (name != Matchup)
                    continue;

                var competitors = eventObj["competitions"]?[0]?["competitors"] as JArray;
                if (competitors == null) continue;

                var team = competitors[Team]["team"];
                if (team == null) continue;

                
                abbreviation = team["abbreviation"]?.ToString();


                break;
            }
            return abbreviation;
        }

        private string getTeamID(String Matchup, int Team, JArray eventsArray)
        {
            string Id = "";

            foreach (JObject eventObj in eventsArray)
            {
                string name = eventObj["name"]?.ToString();
                if (name != Matchup)
                    continue;

                var competitors = eventObj["competitions"]?[0]?["competitors"] as JArray;
                if (competitors == null) continue;

                var team = competitors[Team]["team"];
                if (team == null) continue;


                Id = team["id"]?.ToString();


                break;
            }
            return Id;
        }

        private string getScore(String Matchup, int Team, JArray eventsArray)
        {
            string score = "";

            foreach (JObject eventObj in eventsArray)
            {
                string name = eventObj["name"]?.ToString();
                if (name != Matchup)
                    continue;

                var competitors = eventObj["competitions"]?[0]?["competitors"] as JArray;
                if (competitors == null) continue;

                var team = competitors[Team]["score"];
                if (team == null) continue;

                //Debug.Print(team.ToString());

                score = team.ToString();


                break;
            }
            return score;
        }

        private string getClock(String Matchup, JArray eventsArray)
        {
            string clock = "";

            foreach (JObject eventObj in eventsArray)
            {
                string name = eventObj["name"]?.ToString();
                if (name != Matchup)
                    continue;

                var competitors = eventObj["competitions"] as JArray;
                if (competitors == null) continue;

                var competition = competitors[0] as JObject;
                var status = competition["status"] as JObject;
                var type = status["type"] as JObject;
                var finished = type["completed"].ToObject<bool>();
                var detail = type["detail"].ToString();
                
                if (detail.Equals("Halftime"))
                {
                    clock = "HALF";
                    break;
                }

                if (finished == true)
                {
                    clock = "FINAL";
                    break;
                }

                //Debug.Print(status.ToString());

                var displayClock = status["displayClock"];
                if (displayClock == null) continue;

                clock = displayClock.ToString();

                break;
            }
            return clock;
        }

        private void updateTimeoutScoreboard(int team, JArray events)
        {
            int timeouts = getTimeOuts(gameName, team, events);
            Image timeOuts1 = null;
            Image timeOuts2 = null;
            Image timeOuts3 = null;

            if (team == 0)
            {
                timeOuts1 = (Image)RootGrid.Children
                    .OfType<Image>()
                    .FirstOrDefault(im => im.Name == "TeamTimeOut1L");
                timeOuts2 = (Image)RootGrid.Children
                    .OfType<Image>()
                    .FirstOrDefault(im => im.Name == "TeamTimeOut2L");
                timeOuts3 = (Image)RootGrid.Children
                    .OfType<Image>()
                    .FirstOrDefault(im => im.Name == "TeamTimeOut3L");
            }
            else if (team == 1)
            {
                timeOuts1 = (Image)RootGrid.Children
                    .OfType<Image>()
                    .FirstOrDefault(im => im.Name == "TeamTimeOut1R");
                timeOuts2 = (Image)RootGrid.Children
                    .OfType<Image>()
                    .FirstOrDefault(im => im.Name == "TeamTimeOut2R");
                timeOuts3 = (Image)RootGrid.Children
                    .OfType<Image>()
                    .FirstOrDefault(im => im.Name == "TeamTimeOut3R");
            }

            switch (timeouts)
            {
                case 1:
                    timeOuts1.Visibility = Visibility.Visible;
                    timeOuts2.Visibility = Visibility.Hidden;
                    timeOuts3.Visibility = Visibility.Hidden;
                    break ;
                case 2:
                    timeOuts1.Visibility = Visibility.Visible;
                    timeOuts2.Visibility = Visibility.Visible;
                    timeOuts3.Visibility = Visibility.Hidden;
                    break;
                case 3:
                    timeOuts1.Visibility = Visibility.Visible;
                    timeOuts2.Visibility = Visibility.Visible;
                    timeOuts3.Visibility = Visibility.Visible;
                    break;
                default:
                    timeOuts1.Visibility = Visibility.Hidden;
                    timeOuts2.Visibility = Visibility.Hidden;
                    timeOuts3.Visibility = Visibility.Hidden;
                    break;
            }
        }

        private int getTimeOuts(String Matchup, int team, JArray eventsArray)
        {
            int timeouts = 0;

            foreach (JObject eventObj in eventsArray)
            {
                string name = eventObj["name"]?.ToString();
                //Debug.WriteLine(name);
                //Debug.WriteLine(Matchup);

                if (name != Matchup)
                    continue;

                var competitors = eventObj["competitions"] as JArray;
                if (competitors == null) continue;

                var competition = competitors[0] as JObject;

                var status = competition["situation"] as JObject;
                if (status == null) continue;

                if (team == 1)
                {
                    timeouts = status["homeTimeouts"].ToObject<int>();
                }
                else if (team == 0)
                {
                    timeouts = status["awayTimeouts"].ToObject<int>();
                }

                break;
            }
            return timeouts;
        }

        private string getDownDistance(String Matchup, JArray eventsArray)
        {
            string DownDistance = "";

            foreach (JObject eventObj in eventsArray)
            {
                string name = eventObj["name"]?.ToString();
                //Debug.WriteLine(name);
                //Debug.WriteLine(Matchup);

                if (name != Matchup)
                    continue;

                var competitors = eventObj["competitions"] as JArray;
                if (competitors == null) continue;

                var competition = competitors[0] as JObject;

                var status = competition["situation"] as JObject;
                if (status == null) continue;

                var downDistanceText = status["shortDownDistanceText"];
                if (downDistanceText == null) continue;

                DownDistance = downDistanceText.ToString();

                break;
            }
            return DownDistance;
        }

        private string getPossession(String Matchup, JArray eventsArray)
        {
            string PossessionID = "";

            foreach (JObject eventObj in eventsArray)
            {
                string name = eventObj["name"]?.ToString();
                //Debug.WriteLine(name);
                //Debug.WriteLine(Matchup);

                if (name != Matchup)
                    continue;

                var competitors = eventObj["competitions"] as JArray;
                if (competitors == null) continue;

                var competition = competitors[0] as JObject;

                var status = competition["situation"] as JObject;
                if (status == null) continue;

                var possessionText = status["possession"];
                if (possessionText == null) continue;

                PossessionID = possessionText.ToString();

                break;
            }
            return PossessionID;
        }


        private async void updateScoreboard()
        {
            JArray events = await getEvents(urlDate, league);
            string newPeriod = await getPeriod(gameName, events);
            string newScoreHome = getScore(gameName, 1, events);
            string newScoreAway = getScore(gameName, 0, events);
            string newTime = getClock(gameName, events);
            string newDowns = getDownDistance(gameName, events);
            string posessionID = getPossession(gameName, events);
            //Debug.WriteLine("updated");

            TextBox homeScore = (TextBox)RootGrid.Children
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Name == "TeamScore1");

            if (homeScore != null)
                homeScore.Text = newScoreHome;

            TextBox awayScore = (TextBox)RootGrid.Children
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Name == "TeamScore2");

            if (awayScore != null)
                awayScore.Text = newScoreAway;

            TextBox scoreClock = (TextBox)RootGrid.Children
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Name == "tickerClock");

            if (scoreClock != null)
                scoreClock.Text = newTime;

            TextBox period = (TextBox)RootGrid.Children
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Name == "tickerQuarter");

            if (period != null)
                period.Text = newPeriod;

            TextBox downs = (TextBox)RootGrid.Children
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Name == "tickerPlay");

            if (downs != null)
                downs.Text = newDowns;

            Image PosessionL = (Image)RootGrid.Children
                .OfType<Image>()
                .FirstOrDefault(im => im.Name == "TeamPossession1");

            Image PosessionR = (Image)RootGrid.Children
                .OfType<Image>()
                .FirstOrDefault(im => im.Name == "TeamPossession2");


            string team0ID = getTeamID(gameName, 0, events);
            string team1ID = getTeamID(gameName, 1, events);

            //Debug.WriteLine(team0ID);
            //Debug.WriteLine(posessionID);

            if (posessionID.Equals(team1ID)){
                PosessionL.Visibility = Visibility.Visible;
                PosessionR.Visibility = Visibility.Hidden;
            }
            else if (posessionID.Equals(team0ID)){
                PosessionR.Visibility = Visibility.Visible;
                PosessionL.Visibility = Visibility.Hidden;
            }
            else
            {
                PosessionR.Visibility = Visibility.Hidden;
                PosessionL.Visibility = Visibility.Hidden;
            }

            updateTimeoutScoreboard(0, events);
            updateTimeoutScoreboard(1, events);
        }

        private async Task<string?> getPeriod(String Matchup, JArray eventsArray)
        {
            string period = "";

            foreach (JObject eventObj in eventsArray)
            {
                string name = eventObj["name"]?.ToString();
                if (name != Matchup)
                    continue;

                var competitors = eventObj["competitions"] as JArray;
                var competition = competitors[0] as JObject;
                var status = competition["status"] as JObject;

                //Debug.Print(status.ToString());

                if (competitors == null) continue;

                var periodJSON = status["period"];
                if (periodJSON == null) continue;
                int periodNUM = periodJSON.ToObject<int>();

                switch (periodNUM)
                {
                    case 0:
                        break;
                    case <= 4:
                        period = periodNUM + getEndNumberModifier(periodNUM);
                        break;
                    case 5:
                        period = "OT";
                        break;
                    default:
                        periodNUM = periodNUM - 4;
                        period = (periodNUM) + getEndNumberModifier(periodNUM) + " OT";
                        break;
                }
                
                break;
            }
            return period;
        }

        private string getEndNumberModifier(int number)
        {
            number = number % 10;
            String modifier = "";
            switch (number)
            {
                case 1:
                    modifier = "st";
                    break;
                case 2:
                    modifier = "nd";
                    break;
                case 3:
                    modifier = "rd";
                    break;
                default:
                    modifier = "th";
                    break;
            }
            return modifier;
        }

        private async Task<BitmapImage> getTeamLogo(String Matchup, int Team, JArray eventsArray)
        {
            string logoUrl = "";
            foreach (JObject eventObj in eventsArray)
            {
                string name = eventObj["name"]?.ToString();
                if (name != Matchup)
                    continue;

                var competitors = eventObj["competitions"]?[0]?["competitors"] as JArray;
                if (competitors == null) continue;

                var team = competitors[Team]["team"];
                if (team == null) continue;

                logoUrl = team["logo"]?.ToString();
                
                break;
            }
            using HttpClient httpClient = new();

            Debug.WriteLine(logoUrl);

            byte[] imageBytes = await httpClient.GetByteArrayAsync(logoUrl);

            BitmapImage fillBitmap = new BitmapImage();

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

        private async void ClockLoaded(object sender, RoutedEventArgs e)
        {

            JArray Events = await getEvents(urlDate, league);
            var gameClock = getClock(gameName, Events);

            ReplaceSquareInImageWithTextBox(TickerClock, "tickerClock");


            TextBox found = (TextBox)RootGrid.Children
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Name == "tickerClock");

            if (found != null)
                found.Text = gameClock;

            AddTextOutline(found, Colors.Black, 10.0);
        }

        private async void TeamPossessionLoadedL(object sender, RoutedEventArgs e)
        {
            Image found = (Image)RootGrid.Children
                .OfType<Image>()
                .FirstOrDefault(im => im.Name == "TeamPossession1");
            if (found != null)
                found.Visibility = Visibility.Hidden;
        }
        private async void TeamPossessionLoadedR(object sender, RoutedEventArgs e)
        {
            Image found = (Image)RootGrid.Children
                .OfType<Image>()
                .FirstOrDefault(im => im.Name == "TeamPossession2");
            if (found != null)
                found.Visibility = Visibility.Hidden;
        }

        private async void QuarterLoaded(object sender, RoutedEventArgs e)
        {

            JArray Events = await getEvents(urlDate, league);
            var gameQuarter = await getPeriod(gameName, Events);

            ReplaceSquareInImageWithTextBox(TickerQuarter, "tickerQuarter");


            TextBox found = (TextBox)RootGrid.Children
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Name == "tickerQuarter");

            if (found != null)
                found.Text = gameQuarter;

            AddTextOutline(found, Colors.Black, 10.0);
        }

        private async void PlayLoaded(object sender, RoutedEventArgs e)
        {

            JArray Events = await getEvents(urlDate, league);
            var DownText = getDownDistance(gameName, Events);

            ReplaceSquareInImageWithTextBox(TickerPlay, "tickerPlay");


            TextBox found = (TextBox)RootGrid.Children
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Name == "tickerPlay");

            if (found != null)
                found.Text = DownText;

            AddTextOutline(found, Colors.Black, 10.0);
        }

        //Start color changing group 
        

        private async void BGColorLoadedL(object sender, RoutedEventArgs e)
        {
            JArray Events = await getEvents(urlDate, league);
            var TeamColor1 = System.Drawing.ColorTranslator.FromHtml(getTeamColor(gameName, 1, Events));
            RecolorImageWithAlpha(BackGroundTeamColor1, TeamColor1);
        }
        private async void BGColorLoadedR(object sender, RoutedEventArgs e)
        {
            JArray Events = await getEvents(urlDate, league);
            var TeamColor1 = System.Drawing.ColorTranslator.FromHtml(getTeamColor(gameName, 0, Events));
            RecolorImageWithAlpha(BackGroundTeamColor2, TeamColor1);
        }
        private async void TeamLogoLoadedL(object sender, RoutedEventArgs e)
        {
            JArray Events = await getEvents(urlDate, league);
            BitmapImage fillBitmap = await getTeamLogo(gameName, 1, Events);
            FillImageWithImageMask(TeamLogo1, new Image { Source = fillBitmap });
        }
        private async void TeamLogoLoadedR(object sender, RoutedEventArgs e)
        {
            JArray Events = await getEvents(urlDate, league);
            BitmapImage fillBitmap = await getTeamLogo(gameName, 0, Events);
            FillImageWithImageMask(TeamLogo2, new Image { Source = fillBitmap });
        }
        private async void TeamNameLoadedL(object sender, RoutedEventArgs e)
        {
            JArray Events = await getEvents(urlDate, league);
            var Team1Abr = getAbbreviation(gameName, 1, Events);
            ReplaceSquareInImageWithTextBox(TeamName1, "TeamName1ABR");

            TextBox found = (TextBox)RootGrid.Children
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Name == "TeamName1ABR");

            if (found != null)
            {
                found.Text = Team1Abr;
                AddTextOutline(found, Colors.Black, 10.0);
            }

        }
        private async void TeamNameLoadedR(object sender, RoutedEventArgs e)
        {
            JArray Events = await getEvents(urlDate, league);
            var Team1Abr = getAbbreviation(gameName, 0, Events);
            ReplaceSquareInImageWithTextBox(TeamName2, "TeamName2ABR");

            TextBox found = (TextBox)RootGrid.Children
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Name == "TeamName2ABR");

            if (found != null)
            {
                found.Text = Team1Abr;
                AddTextOutline(found, Colors.Black, 10.0);
            }
        }

        private async void TeamScoreLoadedL(object sender, RoutedEventArgs e)
        {
            JArray Events = await getEvents(urlDate, league);
            var Team1Score = getScore(gameName, 1, Events);
            ReplaceSquareInImageWithTextBox(TeamScore1, "TeamScore1");

            TextBox found = (TextBox)RootGrid.Children
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Name == "TeamScore1");

            if (found != null)
            {
                found.Text = Team1Score;
                AddTextOutline(found, Colors.Black, 10.0);
            }

        }
        private async void TeamScoreLoadedR(object sender, RoutedEventArgs e)
        {
            JArray Events = await getEvents(urlDate, league);
            var Team2Score = getScore(gameName, 0, Events);
            ReplaceSquareInImageWithTextBox(TeamScore2, "TeamScore2");

            TextBox found = (TextBox)RootGrid.Children
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Name == "TeamScore2");

            if (found != null)
            {
                found.Text = Team2Score;
                AddTextOutline(found, Colors.Black, 10.0);
            }

        }
        //end color change group
    }
}
