using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace HikariEditor;

public sealed partial class MainWindow : Window
{
    public Editor? editor;
    public Terminal? terminal;
    public StackPanel? logTabPanel;

    static string EditorDir => $"{Path.GetTempPath()}HikariEditor";

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

    async void EditorSetup()
    {
        StatusBar.Text = "エディタの初期設定中...";
        await EditorSetupAsync();
        CopyEditorFile();
        StatusBar.Text = "エディタの初期設定、完了";
        await Task.Delay(2000);
        StatusBar.Text = "";
    }

    static async Task EditorSetupAsync()
    {
        // エディタの初期設定
        if (!Directory.Exists(EditorDir))
            Directory.CreateDirectory(EditorDir);

        string editorUri = @"https://registry.npmjs.org/monaco-editor/-/monaco-editor-0.38.0.tgz";
        string downloadFile = $"{EditorDir}\\{Path.GetFileName(editorUri)}";
        if (!File.Exists(downloadFile))
        {
            HttpClient client = new();
            HttpResponseMessage response = await client.GetAsync(editorUri);
            if (response.StatusCode != HttpStatusCode.OK)
                return;
            using Stream stream = await response.Content.ReadAsStreamAsync();
            using FileStream dst = File.Create(downloadFile);
            stream.CopyTo(dst);
        }

        if (!Directory.Exists($"{EditorDir}\\editor"))
        {
            FileItem editorCp = new(downloadFile);
            editorCp.Extract();
            Directory.Move($"{EditorDir}\\package", $"{EditorDir}\\editor");
        }
    }

    static void CopyEditorFile()
    {
        // 未パッケージ実行では ms-appx が解決できないため、出力ディレクトリから直接コピーする
        string htmlPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Editor.html");
        if (File.Exists(htmlPath) && Directory.Exists($"{EditorDir}\\editor"))
            File.Copy(htmlPath, $"{EditorDir}\\editor\\index.html", true);
    }

    // 開くをクリック
    void OpenClick(object sender, RoutedEventArgs e)
    {
        editorFrame.Navigate(typeof(Open), this);
        editorFrame.Height = double.NaN;
    }

    // ターミナルを開く
    // 初期タブの生成は Terminal.OnNavigatedTo に任せる（Navigate 完了前に
    // terminal フィールドへ触れるとレースになるため）
    void ClickOpenTerminal(object sender, RoutedEventArgs e)
    {
        Terminal.ClickOpenTerminal(this);
    }

    void LoadConfig()
    {
        Settings settings = new();
        settings.LoadSetting();
        AutoSave.IsChecked = settings.AutoSave;
        ToggleStyle(AutoSave.IsChecked);

        // 前回ログを開いていた場合は復元する。
        // コンストラクタ段階ではビジュアルツリーが未構築で terminalFrame の
        // Navigate / 高さ変更が反映されないため、ウィンドウ表示後に一度だけ実行する。
        if (settings.LogOpen)
        {
            void RestoreLog(object sender, WindowActivatedEventArgs e)
            {
                Activated -= RestoreLog;
                LogPage.ClickOpenLog(this);
            }
            Activated += RestoreLog;
        }
    }

    void ToggleStyle(bool isOn)
    {
        if (isOn)
        {
            AutoSaveToggleSwitchText.Text = "オン";
            AutoSaveToggleSwitchText.Margin = new Thickness(5, 12.5, 0, 0);
        }
        else
        {
            AutoSaveToggleSwitchText.Text = "オフ";
            AutoSaveToggleSwitchText.Margin = new Thickness(19, 12.5, 0, 0);
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
        if (sender.SelectedItem is not NavigationViewItem selectedItem) return;
        if ((string)selectedItem.Tag == "Explorer")
        {
            if (SideMenuEditorArea.ColumnDefinitions[0].Width.Value == 360)
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
            editor!.CallPasteFunction(text);
        }
    }

    private void ClickCopyText(object sender, RoutedEventArgs e)
    {
        editor!.CallCopyFunction();
    }

    private async void ClickAboutDialog(object sender, RoutedEventArgs e)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot,
            Title = "ひかりエディタ",
            PrimaryButtonText = "OK",
            DefaultButton = ContentDialogButton.Primary,
            Content = new About()
        };
        await dialog.ShowAsync();
    }

    private void ClickOpenLog(object sender, RoutedEventArgs e)
    {
        LogPage.ClickOpenLog(this);
    }
}
