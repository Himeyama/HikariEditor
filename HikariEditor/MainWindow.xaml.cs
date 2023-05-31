using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Svgicon5;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using WinRT.Interop;

namespace HikariEditor
{
    public sealed partial class MainWindow : Window
    {
        public Editor editor;
        ApplicationDataContainer config;


        public MainWindow()
        {
            InitializeComponent();
            configSetup();
            loadConfig();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            Manager.mainWindow = this;
            Manager.contentFrame = contentFrame;

            TitleBar titleBar = new(this, "Hikari Editor");
            setWindowSize(1920, 1200);
            editorFrame.Navigate(typeof(Editor), this);
            config.Values["explorerDir"] = "";
            OpenExplorer.IsEnabled = false;
            editorSetup();
        }

        void setWindowSize(int width, int height)
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId myWndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(myWndId);
            appWindow.Resize(new SizeInt32(width, height));
        }

        async void editorSetup()
        {
            StatusBar.Text = "エディタの初期設定中...";
            await editorSetupAsync();
            _ = copyEditorFile();
            StatusBar.Text = "エディタの初期設定、完了";
            await Task.Delay(2000);
            StatusBar.Text = "";
        }

        async Task editorSetupAsync()
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
                editorCp.extract();
                Directory.Move($"{editorDir}\\package", $"{editorDir}\\editor");
            }
        }

        async Task copyEditorFile()
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
        }

        void configSetup()
        {
            config = ApplicationData.Current.LocalSettings;
            config.Values["AutoSave"] ??= false;
        }

        void loadConfig()
        {
            AutoSave.IsChecked = (bool)config.Values["AutoSave"];
            ToggleStyle(AutoSave.IsChecked);
        }

        void ToggleStyle(bool isOn)
        {
            if (isOn)
            {
                AutoSaveToggleSwitchText.Text = "オン";
                AutoSaveToggleSwitchText.Foreground = new SolidColorBrush(Colors.Black);
                AutoSaveToggleSwitchText.Margin = new Thickness(5, 12.5, 0, 0);
            }
            else
            {
                AutoSaveToggleSwitchText.Text = "オフ";
                AutoSaveToggleSwitchText.Margin = new Thickness(19, 12.5, 0, 0);
                AutoSaveToggleSwitchText.Foreground = AppTitleBar.ActualTheme == ElementTheme.Light ? new SolidColorBrush(Colors.Black) : (Brush)new SolidColorBrush(Colors.White);
            }
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            config.Values["AutoSave"] = AutoSaveToggleSwitch.IsOn;
            ToggleStyle(AutoSaveToggleSwitch.IsOn);
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
                if (SideMenuEditorArea.ColumnDefinitions[0].Width.ToString() == "336")
                {
                    SideMenuEditorArea.ColumnDefinitions[0].Width = new GridLength(48);
                    ItemExplorer.IsSelected = false;
                    OpenExplorer.IsEnabled = false;
                }
                else
                {
                    SideMenuEditorArea.ColumnDefinitions[0].Width = new GridLength(336);
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
            string explorerDir = config.Values["explorerDir"] as string;
            if (explorerDir != "")
                Process.Start("explorer.exe", explorerDir);
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
    }
}
