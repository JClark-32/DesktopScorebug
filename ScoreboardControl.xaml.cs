using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Newtonsoft.Json.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace Desktop_Scorebug_WPF
{
    /// <summary>
    /// Interaction logic for ScoreboardControl.xaml
    /// </summary>
    public partial class ScoreboardControl : Window
    {

        private FootballScoreboard _scoreboard;
        private NascarTicker _ticker;

        public ScoreboardControl()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            createButtons();
            this.Closed += OnParentWindowClosed;
            
        }

        public string FirstLetterToUpper(string str)
        {
            if (str == null)
                return "";

            if (str.Length > 1)
                return char.ToUpper(str[0]) + str.Substring(1);

            return str.ToUpper();
        }
        private async void createButtons()
        {
            string todayURLFormatted = DateTime.Today.ToString("yyyyMMdd");
            string yesterdayURLFormatted = DateTime.Today.AddDays(-1).ToString("yyyyMMdd");
            string daySpread = yesterdayURLFormatted + "-" + todayURLFormatted;

            string urlDate = "20241129";
            //daySpread = urlDate;

            JArray nflEvents = await getEventsArray(daySpread, "nfl");
            JArray cfbEvents = await getEventsArray(daySpread, "college-football");

            JArray nflArray = getEventNames(nflEvents);
            JArray cfbArray = getEventNames(cfbEvents);

            //CreateButtonsFromJArray(getActiveArray(nflArray, nflEvents), "nfl", daySpread);
            //CreateButtonsFromJArray(getActiveArray(cfbArray, cfbEvents), "college-football", daySpread);
     
            CreateButtonsFromJArray(nflArray, nflEvents, "nfl", daySpread);
            CreateButtonsFromJArray(cfbArray, cfbEvents, "college-football", daySpread);
            CreateButtonsFromJArray(["Testing"], [], "nascar", daySpread);

            CreateRefreshButton();
        }

        private JArray getActiveArray(JArray namesArray, JArray eventsArray)
        {
            JArray activeArray = new JArray();

            foreach (var Event in namesArray)
            {
                string gameName = Event.ToString();
                if (getMatchStatus(gameName, eventsArray).Equals("true")){
                    activeArray.Add(gameName);
                }
                else
                {
                    Debug.WriteLine(getMatchStatus(gameName, eventsArray).ToString());
                }
            }
            return activeArray;
        }

        private string getMatchStatus(String Matchup, JArray eventsArray)
        {
            string active = "true";

            foreach (JObject eventObj in eventsArray)
            {
                string name = eventObj["name"]?.ToString();
                if (name != Matchup)
                    continue;

                var competitors = eventObj["competitions"] as JArray;
                //if (competitors == null) continue;

                var competition = competitors[0] as JObject;
                //if (competition == null) continue;
                var status = competition["status"] as JObject;
                //if (status == null) continue;
                var type = status["type"] as JObject;
                //if (type == null) continue;
                Debug.WriteLine(type.ToString());

                var completed = type["completed"].ToObject<bool>();
                //if (completed == null) continue;
                var gameState = type["state"].ToString();
                //if (gameState == null) continue;

                Debug.WriteLine("yo");
                var finished = completed;
                

                var state = gameState;

                if (finished == true || state == "pre")
                {
                    active = "false";
                    break;
                }

                break;
            }
            return active;
        }

        private JArray getEventNames(JArray eventsArray)
        {
            JArray names = [];

            foreach (JObject eventObj in eventsArray)
            {
                // This grabs the "name" property if it exists directly in the event object
                JToken nameToken = eventObj["name"];
                if (nameToken != null)
                {
                    names.Add(nameToken.ToString());
                }
            }
            //Debug.WriteLine(names.ToString());
            return names;
        }

        private async Task<JArray> getEventsArray(String urlDate, String league)
        {
            JObject json = await getJsonfromEndpoint(urlDate, league);
            JArray array = (JArray)json["events"];
            if (array == null) 
                return [];
            return array;
        }

        private void CreateRefreshButton()
        {
            if (MenuBar.Children.Count == 0)
            {

                Button button = new Button
                {
                    Name = "refreshButton",
                    Content = "↻",
                    FontWeight = FontWeights.Bold,
                    FontSize = 20,
                    Margin = new Thickness(5),
                    Padding = new Thickness(10),
                    MaxWidth = 50,
                    MinWidth = 20,
                    Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Tag = "Refresh"
                };

                button.Click += (sender, e) =>
                {
                    RefreshClick();
                };

                MenuBar.Children.Add(button);
            }
        }

        private async void CreateButtonsFromJArray(JArray jsonArray, JArray eventsArray, string league, string date)
        {
            if (jsonArray.Count != 0)
            {
                String displayLeague = "";
                switch (league)
                {
                    case "college-football":
                        displayLeague = "College Football";
                        break;
                    case "nfl":
                        displayLeague = "NFL";
                        break;
                    case "nascar":
                        displayLeague = "Nascar Testing";
                        break;
                }
                // Add section header
                TextBlock headerText = new TextBlock
                {
                    Text = displayLeague,
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(5, 15, 5, 5),
                    Foreground = Brushes.White
                };

                ButtonContainer.Children.Add(headerText);

                foreach (var token in jsonArray)
                {
                    string text = token.ToString();
                    StackPanel buttonGrid = new StackPanel
                    {
                        Orientation = Orientation.Horizontal
                    };

                    Button button = new Button
                    {
                        Content = text,
                        Margin = new Thickness(5),
                        Padding = new Thickness(10),
                        MaxWidth = 500,
                        MinWidth = 20,
                        Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                        Foreground = Brushes.White,
                        Tag = text
                    };

                    button.Click += (sender, e) =>
                    {
                        if (_scoreboard != null)
                        {
                            _scoreboard.Close();
                        }
                        if (_ticker != null) 
                        {
                            _ticker.Close();
                        }
                        if (league.Equals("nfl") || league.Equals("college-football"))
                        {
                            _scoreboard = new FootballScoreboard(league, text, date);
                            _scoreboard.Closed += (s, args) => _scoreboard = null;
                            _scoreboard.Show();
                        }
                        if (league.Equals("nascar"))
                        {
                            _ticker = new NascarTicker();
                            _ticker.Closed += (s, args) => _ticker = null;
                            _ticker.Show();
                        }

                        Button closeButton = new Button
                        {
                            Name = "closeButton",
                            Content = "Close",
                            Margin = new Thickness(5),
                            Padding = new Thickness(10),
                            MaxWidth = 500,
                            MinWidth = 20,
                            Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                            Foreground = Brushes.White
                        };

                        if (MenuBar.Children.Count != 2)
                        {
                            Debug.WriteLine(MenuBar.Children[0]);
                            MenuBar.Children.Add(closeButton);
                        }

                        closeButton.Click += (sender, e) =>
                        {
                            if (_scoreboard != null)
                            {
                                _scoreboard.Close();
                            }
                            if (_ticker != null)
                            {
                                _ticker.Close();
                            }
                            MenuBar.Children.Remove(closeButton);
                        };
                    };

                    TextBlock LiveText = new TextBlock
                    {
                        Margin = new Thickness(5),
                        Padding = new Thickness(10),
                        MaxWidth = 500,
                        MinWidth = 20,
                        Text = "⦿ Live",
                        FontSize = 15,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.Red
                    };

                    buttonGrid.Children.Add(button);
                    if (getMatchStatus(text, eventsArray).Equals("true")){
                        if(!league.Equals("nascar"))
                        buttonGrid.Children.Add(LiveText);
                    }

                    ButtonContainer.Children.Add(buttonGrid);
                }
            }
        }

        private void RefreshClick()
        {
            ButtonContainer.Children.Clear();
            createButtons();
        }


        private static async Task<JObject> getJsonfromEndpoint(String urlDate, String league)
        {
            string url = "https://site.api.espn.com/apis/site/v2/sports/football/" + league + "/scoreboard?dates=" + urlDate;
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
            return [];
        }
        private void OnParentWindowClosed(object? sender, EventArgs e)
        {
            if (_scoreboard != null)
            {
                _scoreboard.Close();
                //_scoreboard = null;
            }
            if (_ticker != null)
            {
                _ticker.Close();
            }

        }

    }
}
