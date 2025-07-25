using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Newtonsoft.Json.Linq;

namespace Desktop_Scorebug_WPF
{
    /// <summary>
    /// Interaction logic for ScoreboardControl.xaml
    /// </summary>
    public partial class ScoreboardControl : Window
    {

        private Scoreboard _scoreboard;
        public ScoreboardControl()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            createButtons();
            this.Closed += OnParentWindowClosed;
            
        }
        private async void createButtons()
        {
            string urlDate = "20241129";
            CreateButtonsFromJArray(await getEvents(urlDate, "college-football"));
        }
        private async Task<JArray> getEvents(String urlDate, String league)
        {
            JObject json = await getJsonfromEndpoint(urlDate, league);
            JArray array = (JArray)json["events"];
            JArray names = [];

            foreach (JObject eventObj in array)
            {
                // This grabs the "name" property if it exists directly in the event object
                JToken nameToken = eventObj["name"];
                if (nameToken != null)
                {
                    names.Add(nameToken.ToString());
                }
            }
            Debug.WriteLine(names.ToString());
            return names;
        }

        private void CreateButtonsFromJArray(JArray jsonArray)
        {
            ButtonContainer.Children.Clear(); // Assuming ButtonContainer is your StackPanel or similar

            foreach (var token in jsonArray)
            {
                string text = token.ToString();

                Button button = new Button
                {
                    Content = text,
                    Margin = new Thickness(5),
                    Padding = new Thickness(10),
                    Width = 500,
                    Tag = text
                };

                button.Click += (sender, e) =>
                {
                    if (_scoreboard != null)
                    {
                        _scoreboard.Close();  // Close existing window
                        _scoreboard = null;   // Clear reference
                    }

                    _scoreboard = new Scoreboard("college-football", text);
                    _scoreboard.Closed += (s, args) => _scoreboard = null;
                    _scoreboard.Show();
                    //MessageBox.Show($"You clicked: {text}");
                };

                ButtonContainer.Children.Add(button);
            }
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
            return null;
        }
        private void OnParentWindowClosed(object? sender, EventArgs e)
        {
            if (_scoreboard != null)
            {
                _scoreboard.Close();
                _scoreboard = null;
            }
        }

    }
}
