using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ModAssistant.Pages;
using static ModAssistant.Http;

namespace ModAssistant
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow Instance;
        public static bool ModsOpened = false;
        public static bool ModsLoading = false;
        public static string GameVersion;
        public static string GameVersionOverride;
        public TaskCompletionSource<bool> VersionLoadStatus = new TaskCompletionSource<bool>();

        public string MainText
        {
            get
            {
                return MainTextBlock.Text;
            }
            set
            {
                Dispatcher.Invoke(new Action(() => { MainWindow.Instance.MainTextBlock.Text = value; }));
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            const int ContentWidth = 1280;
            const int ContentHeight = 720;

            double ChromeWidth = SystemParameters.WindowNonClientFrameThickness.Left + SystemParameters.WindowNonClientFrameThickness.Right;
            double ChromeHeight = SystemParameters.WindowNonClientFrameThickness.Top + SystemParameters.WindowNonClientFrameThickness.Bottom;
            double ResizeBorder = SystemParameters.ResizeFrameVerticalBorderWidth;

            Width = ChromeWidth + ContentWidth + 2 * ResizeBorder;
            Height = ChromeHeight + ContentHeight + 2 * ResizeBorder;

            VersionText.Text = App.Version;

            Themes.LoadThemes();
            Themes.FirstLoad(Properties.Settings.Default.SelectedTheme);

            Task.Run(() => LoadVersionsAsync());

            if (!Properties.Settings.Default.Agreed || string.IsNullOrEmpty(Properties.Settings.Default.LastTab))
            {
                Main.Content = Intro.Instance;
            }
            else
            {
                switch (Properties.Settings.Default.LastTab)
                {
                    case "Intro":
                        Main.Content = Intro.Instance;
                        break;
                    case "Mods":
                        _ = ShowModsPage();
                        break;
                    case "About":
                        Main.Content = About.Instance;
                        break;
                    case "Options":
                        Main.Content = Options.Instance;
                        Themes.LoadThemes();
                        break;
                    default:
                        Main.Content = Intro.Instance;
                        break;
                }
            }
        }

        private async void LoadVersionsAsync()
        {
            try
            {
                var resp = await HttpClient.GetAsync(Utils.Constants.BeatModsVersions);
                var body = await resp.Content.ReadAsStringAsync();
                List<string> versions = JsonSerializer.Deserialize<string[]>(body).ToList();

                resp = await HttpClient.GetAsync(Utils.Constants.BeatModsAlias);
                body = await resp.Content.ReadAsStringAsync();
                object jsonObject = JsonSerializer.DeserializeObject(body);

                Dispatcher.Invoke(() =>
                {
                    GameVersion = GetGameVersion(versions, jsonObject);

                    GameVersionsBox.ItemsSource = versions;
                    GameVersionsBox.SelectedValue = GameVersion;

                    if (!string.IsNullOrEmpty(GameVersionOverride))
                    {
                        GameVersionsBox.Visibility = Visibility.Collapsed;
                        GameVersionsBoxOverride.Visibility = Visibility.Visible;
                        GameVersionsBoxOverride.Text = GameVersionOverride;
                        GameVersionsBoxOverride.IsEnabled = false;
                    }

                    if (!string.IsNullOrEmpty(GameVersion) && Properties.Settings.Default.Agreed)
                    {
                        MainWindow.Instance.ModsButton.IsEnabled = true;
                    }
                });

                VersionLoadStatus.SetResult(true);
            }
            catch (Exception e)
            {
                Dispatcher.Invoke(() =>
                {
                    GameVersionsBox.IsEnabled = false;
                    MessageBox.Show($"{Application.Current.FindResource("MainWindow:GameVersionLoadFailed")}\n{e}");
                });

                VersionLoadStatus.SetResult(false);
            }
        }

        private string GetGameVersion(List<string> versions, object aliases)
        {
            string version = Utils.GetVersion();
            if (!string.IsNullOrEmpty(version) && versions.Contains(version))
            {
                return version;
            }

            string aliasOf = CheckAliases(versions, aliases, version);
            if (!string.IsNullOrEmpty(aliasOf))
            {
                return aliasOf;
            }

            string versionsString = String.Join(",", versions.ToArray());
            if (Properties.Settings.Default.AllGameVersions != versionsString)
            {
                Properties.Settings.Default.AllGameVersions = versionsString;
                Properties.Settings.Default.Save();

                string title = (string)Application.Current.FindResource("MainWindow:GameUpdateDialog:Title");
                string line1 = (string)Application.Current.FindResource("MainWindow:GameUpdateDialog:Line1");
                string line2 = (string)Application.Current.FindResource("MainWindow:GameUpdateDialog:Line2");

                Utils.ShowMessageBoxAsync($"{line1}\n\n{line2}", title);
                return versions[0];
            }

            if (!string.IsNullOrEmpty(Properties.Settings.Default.GameVersion) && versions.Contains(Properties.Settings.Default.GameVersion))
                return Properties.Settings.Default.GameVersion;
            return versions[0];
        }

        private string CheckAliases(List<string> versions, object aliases, string detectedVersion)
        {
            Dictionary<string, object> Objects = (Dictionary<string, object>)aliases;
            foreach (string version in versions)
            {
                object[] aliasArray = (object[])Objects[version];
                foreach (object alias in aliasArray)
                {
                    if (alias.ToString() == detectedVersion)
                    {
                        GameVersionOverride = detectedVersion;
                        return version;
                    }
                }
            }
            return string.Empty;
        }

        private async Task ShowModsPage()
        {
            void OpenModsPage()
            {
                Main.Content = Mods.Instance;
                Properties.Settings.Default.LastTab = "Mods";
                Properties.Settings.Default.Save();
                Mods.Instance.RefreshColumns();
            }

            if (ModsOpened == true && Mods.Instance.PendingChanges == false)
            {
                OpenModsPage();
                return;
            }

            Main.Content = Loading.Instance;

            if (ModsLoading) return;
            ModsLoading = true;
            await Mods.Instance.LoadMods();
            ModsLoading = false;

            if (ModsOpened == false) ModsOpened = true;
            if (Mods.Instance.PendingChanges == true) Mods.Instance.PendingChanges = false;

            if (Main.Content == Loading.Instance)
            {
                OpenModsPage();
            }
        }

        private void ModsButton_Click(object sender, RoutedEventArgs e)
        {
            _ = ShowModsPage();
        }

        private void IntroButton_Click(object sender, RoutedEventArgs e)
        {
            Main.Content = Intro.Instance;
            Properties.Settings.Default.LastTab = "Intro";
            Properties.Settings.Default.Save();
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            Main.Content = About.Instance;
            Properties.Settings.Default.LastTab = "About";
            Properties.Settings.Default.Save();
        }

        private void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            Main.Content = Options.Instance;
            Themes.LoadThemes();
            Properties.Settings.Default.LastTab = "Options";
            Properties.Settings.Default.Save();
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            Mods.Instance.InstallMods();
        }

        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            if ((Mods.ModListItem)Mods.Instance.ModsListView.SelectedItem == null)
            {
                MessageBox.Show((string)Application.Current.FindResource("MainWindow:NoModSelected"));
                return;
            }
            Mods.ModListItem mod = ((Mods.ModListItem)Mods.Instance.ModsListView.SelectedItem);
            string infoUrl = mod.ModInfo.link;
            if (string.IsNullOrEmpty(infoUrl))
            {
                MessageBox.Show(string.Format((string)Application.Current.FindResource("MainWindow:NoModInfoPage"), mod.ModName));
            }
            else
            {
                System.Diagnostics.Process.Start(infoUrl);
            }
        }

        private async void GameVersionsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string oldGameVersion = GameVersion;

            GameVersion = (sender as ComboBox).SelectedItem.ToString();

            if (string.IsNullOrEmpty(oldGameVersion)) return;

            Properties.Settings.Default.GameVersion = GameVersion;
            Properties.Settings.Default.Save();

            if (ModsOpened)
            {
                var prevPage = Main.Content;

                Mods.Instance.PendingChanges = true;
                await ShowModsPage();

                Main.Content = prevPage;
            }
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            About.Instance.PatUp.IsOpen = false;
            About.Instance.PatButton.IsEnabled = true;
            About.Instance.HugUp.IsOpen = false;
            About.Instance.HugButton.IsEnabled = true;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Main.Content == Mods.Instance)
            {
                Mods.Instance.RefreshColumns();
            }
        }

        private void BackgroundVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            BackgroundVideo.Position = TimeSpan.Zero;
            BackgroundVideo.Play();
        }
    }
}
