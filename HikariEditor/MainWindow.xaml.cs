using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using WinRT.Interop;

namespace HikariEditor
{
    public sealed partial class MainWindow : Window
    {
        public Editor editor;
        public Terminal terminal;
        public StackPanel logTabPanel;

        public MainWindow()
        {
            InitializeComponent();

            /* タイトルバーの設定 */
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            /* エディタの設定 */
            editorFrame.Navigate(typeof(Editor), this);
            EditorSetup();

            /* エクスプローラーを非表示に */
            OpenExplorer.IsEnabled = false;

            /* 自動保存設定の読み込み */
            LoadConfig();
        }

        void SetWindowSize(int width, int height)
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId myWndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(myWndId);
            appWindow.Resize(new SizeInt32(width, height));
        }

        async void EditorSetup()
        {
            StatusBar.Text = "エディタの初期設定中...";
            await EditorSetupAsync();
            _ = CopyEditorFile();
            StatusBar.Text = "エディタの初期設定、完了";
            await Task.Delay(2000);
            StatusBar.Text = "";
        }

        static async Task EditorSetupAsync()
        {
            // エディタの初期設定
            string tempDirectory = Path.GetTempPath();
            string editorDir = $"{tempDirectory}HikariEditor";
            bool exists = Directory.Exists(editorDir);
            if (!exists)
            {
                Directory.CreateDirectory(editorDir);
            }

            string editorUri = @"https://registry.npmjs.org/monaco-editor/-/monaco-editor-0.38.0.tgz";
            string downloadFile = editorDir + @"\" + Path.GetFileName(editorUri);
            if (!File.Exists(downloadFile))
            {
                HttpClient client = new();
                HttpResponseMessage response = await client.GetAsync(editorUri);
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    return;
                using Stream stream = await response.Content.ReadAsStreamAsync();
                using FileStream dst = File.Create(downloadFile);
                stream.CopyTo(dst);
            }

            if (!Directory.Exists($"{editorDir}\\editor"))
            {
                FileItem editorCp = new(downloadFile);
                editorCp.Extract();
                Directory.Move($"{editorDir}\\package", $"{editorDir}\\editor");
            }
        }

        static async Task CopyEditorFile()
        {
            StorageFile htmlFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Editor.html"));
            string tempDirectory = Path.GetTempPath();
            string editorDir = $"{tempDirectory}HikariEditor";
            if (File.Exists(htmlFile.Path))
            {
                if (Directory.Exists($"{editorDir}\\editor"))
                    File.Copy(htmlFile.Path, $"{editorDir}\\editor\\index.html", true);
            }
        }

        // 開くをクリック
        void OpenClick(object sender, RoutedEventArgs e)
        {
            editorFrame.Navigate(typeof(Open), this);
            editorFrame.Height = double.NaN;
        }

        // ターミナルを開く
        void ClickOpenTerminal(object sender, RoutedEventArgs e)
        {
            Terminal.ClickOpenTerminal(this);
            terminal.AddNewTab(terminal.terminalTabs);
        }

        void LoadConfig()
        {
            Settings settings = new();
            settings.LoadSetting();
            AutoSave.IsChecked = settings.AutoSave;
            ToggleStyle(AutoSave.IsChecked);
        }

        void ToggleStyle(bool isOn)
        {
            if (isOn)
            {
                AutoSaveToggleSwitchText.Text = "オン";
                //AutoSaveToggleSwitchText.Foreground = new SolidColorBrush(Colors.Black);
                AutoSaveToggleSwitchText.Margin = new Thickness(5, 12.5, 0, 0);
            }
            else
            {
                AutoSaveToggleSwitchText.Text = "オフ";
                AutoSaveToggleSwitchText.Margin = new Thickness(19, 12.5, 0, 0);
                //AutoSaveToggleSwitchText.Foreground = AppTitleBar.ActualTheme == ElementTheme.Light ? new SolidColorBrush(Colors.Black) : (Brush)new SolidColorBrush(Colors.White);
            }
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            Settings settings = new();
            settings.LoadSetting();
            settings.AutoSave = AutoSaveToggleSwitch.IsOn;
            ToggleStyle(AutoSaveToggleSwitch.IsOn);
            settings.SaveSetting();
        }

        void ExitClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ClickNLBtn(object sender, RoutedEventArgs e)
        {
            NLBtn.Content = (string)NLBtn.Content == "LF" ? "CRLF" : "LF";
        }

        private void MenuChanged(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            NavigationViewItem selectedItem = sender.SelectedItem as NavigationViewItem;
            if (selectedItem == null) return;
            if ((string)selectedItem.Tag == "Explorer")
            {
                if (SideMenuEditorArea.ColumnDefinitions[0].Width.ToString() == "360")
                {
                    SideMenuEditorArea.ColumnDefinitions[0].Width = new GridLength(48);
                    ItemExplorer.IsSelected = false;
                    OpenExplorer.IsEnabled = false;
                }
                else
                {
                    SideMenuEditorArea.ColumnDefinitions[0].Width = new GridLength(360);
                    ItemExplorer.IsSelected = true;
                    OpenExplorer.IsEnabled = true;
                }

                contentFrame.Navigate(typeof(Explorer), this);
            }
            else if ((string)selectedItem.Tag == "Search")
            {
                contentFrame.Navigate(typeof(Search), this);
            }
        }

        public void ClickOpenExplorer(object sender, RoutedEventArgs e)
        {
            Settings settings = new();
            settings.LoadSetting();

            if (settings.ExplorerDir != "")
                Process.Start("explorer.exe", settings.ExplorerDir);
        }

        async void ClickPasteText(object sender, RoutedEventArgs e)
        {
            DataPackageView dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                string text = await dataPackageView.GetTextAsync();
                editor.CallPasteFunction(text);
            }
        }

        private void ClickCopyText(object sender, RoutedEventArgs e)
        {
            editor.CallCopyFunction();
        }

        async private void ClickAboutDialog(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new();
            dialog.XamlRoot = this.Content.XamlRoot;
            dialog.Title = "ひかりエディタ";
            dialog.PrimaryButtonText = "OK";
            dialog.DefaultButton = ContentDialogButton.Primary;
            dialog.Content = new About();
            await dialog.ShowAsync();
        }

        private void ClickOpenLog(object sender, RoutedEventArgs e)
        {
            LogPage.ClickOpenLog(this);
        }

    }
}
