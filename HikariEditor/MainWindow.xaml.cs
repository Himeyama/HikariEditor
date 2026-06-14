using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

    // 直近のターミナル／ログ領域の高さとエクスプローラーの横幅。
    // 再オープン時やリサイズ後・次回起動時の復元に使う。
    double terminalHeight = 300;
    double explorerWidth = 360;

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

        // 前回のリサイズ結果を復元する
        terminalHeight = settings.TerminalHeight;
        explorerWidth = settings.ExplorerWidth;
        bool restoreLog = settings.LogOpen;

        // ビジュアルツリー構築後に一度だけ復元処理を行う。
        // コンストラクタ段階ではビジュアルツリーが未構築で Frame の
        // Navigate / サイズ変更が反映されないため、ウィンドウ表示後に実行する。
        void Restore(object sender, WindowActivatedEventArgs e)
        {
            Activated -= Restore;
            ShowExplorerPanel();              // エクスプローラーは既定で開いた状態にする
            if (restoreLog) LogPage.ClickOpenLog(this);
        }
        Activated += Restore;
    }

    void ToggleStyle(bool isOn)
    {
        if (isOn)
        {
            AutoSaveToggleSwitchText.Text = "オン";
            AutoSaveToggleSwitchText.Margin = new Thickness(5, 12.5, 0, 0);
            // オン時はトグルがアクセント色で塗られるため白文字で視認性を確保する
            AutoSaveToggleSwitchText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
        }
        else
        {
            AutoSaveToggleSwitchText.Text = "オフ";
            AutoSaveToggleSwitchText.Margin = new Thickness(19, 12.5, 0, 0);
            // オフ時は既定のテーマ色に戻す
            AutoSaveToggleSwitchText.ClearValue(TextBlock.ForegroundProperty);
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
        // 選択中タブの改行コードを切り替え、新しいコードで保存し直す
        editor?.ApplyNewline((string)NLBtn.Content);
    }

    private void MenuChanged(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (sender.SelectedItem is not NavigationViewItem selectedItem) return;
        if ((string)selectedItem.Tag == "Explorer")
        {
            // 開いている（幅がコンパクトレールの 48 より大きい）ときは畳む
            if (SideMenuEditorArea.ColumnDefinitions[0].Width.Value > 48)
                HideExplorerPanel();
            else
                ShowExplorerPanel();
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

    // ターミナル／ログ領域を表示する。高さは直近のサイズを復元する。
    public void ShowTerminal()
    {
        terminalFrame.Height = terminalHeight;
        terminalSplitter.Visibility = Visibility.Visible;
    }

    // ターミナル／ログ領域を畳む。次回表示用に高さは保持しておく。
    public void HideTerminal()
    {
        terminalFrame.Height = 0;
        terminalSplitter.Visibility = Visibility.Collapsed;
    }

    // エクスプローラーを表示する。横幅は直近のサイズを復元する。
    public void ShowExplorerPanel()
    {
        SideMenuEditorArea.ColumnDefinitions[0].Width = new GridLength(explorerWidth);
        explorerSplitter.Visibility = Visibility.Visible;
        ItemExplorer.IsSelected = true;
        OpenExplorer.IsEnabled = true;
        contentFrame.Navigate(typeof(Explorer), this);
    }

    // エクスプローラーを畳む（コンパクトレールのみ表示）。次回表示用に横幅は保持しておく。
    public void HideExplorerPanel()
    {
        SideMenuEditorArea.ColumnDefinitions[0].Width = new GridLength(48);
        explorerSplitter.Visibility = Visibility.Collapsed;
        ItemExplorer.IsSelected = false;
        OpenExplorer.IsEnabled = false;
    }

    // ハンドルのドラッグでターミナル／ログ領域を上下にリサイズする。
    // ドラッグ中はハンドル自身が動いて自己相対座標が揺れるため、
    // 親コンテナ基準の座標で開始位置からの差分を計算する。
    bool resizingTerminal;
    double resizeStartY;
    double resizeStartHeight;

    void TerminalSplitter_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        resizingTerminal = true;
        resizeStartY = e.GetCurrentPoint(RightSideFrames).Position.Y;
        resizeStartHeight = terminalFrame.ActualHeight;
        terminalSplitter.CapturePointer(e.Pointer);
    }

    void TerminalSplitter_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!resizingTerminal) return;

        double y = e.GetCurrentPoint(RightSideFrames).Position.Y;
        // 下に動かすと delta が正。ターミナルは上方向に広げるので符号を反転する。
        double newHeight = resizeStartHeight - (y - resizeStartY);

        // エディタ領域を最低限残しつつ、ターミナルが潰れないようにクランプする
        double max = Math.Max(80, RightSideFrames.ActualHeight - 120);
        terminalHeight = Math.Clamp(newHeight, 80, max);
        terminalFrame.Height = terminalHeight;
    }

    void TerminalSplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!resizingTerminal) return;
        resizingTerminal = false;
        terminalSplitter.ReleasePointerCapture(e.Pointer);
        SaveLayout();
    }

    // ハンドルのドラッグでエクスプローラーの横幅をリサイズする。
    bool resizingExplorer;
    double explorerResizeStartX;
    double explorerResizeStartWidth;

    void ExplorerSplitter_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        resizingExplorer = true;
        explorerResizeStartX = e.GetCurrentPoint(SideMenuEditorArea).Position.X;
        explorerResizeStartWidth = explorerWidth;
        explorerSplitter.CapturePointer(e.Pointer);
    }

    void ExplorerSplitter_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!resizingExplorer) return;

        double x = e.GetCurrentPoint(SideMenuEditorArea).Position.X;
        double newWidth = explorerResizeStartWidth + (x - explorerResizeStartX);

        // エディタ領域を最低限残しつつ、エクスプローラーが潰れないようにクランプする
        double max = Math.Max(150, SideMenuEditorArea.ActualWidth - 200);
        explorerWidth = Math.Clamp(newWidth, 150, max);
        SideMenuEditorArea.ColumnDefinitions[0].Width = new GridLength(explorerWidth);
    }

    void ExplorerSplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!resizingExplorer) return;
        resizingExplorer = false;
        explorerSplitter.ReleasePointerCapture(e.Pointer);
        SaveLayout();
    }

    // リサイズ結果（ターミナル高さ・エクスプローラー幅）を次回起動用に保存する
    void SaveLayout()
    {
        Settings settings = new();
        settings.LoadSetting();
        settings.TerminalHeight = terminalHeight;
        settings.ExplorerWidth = explorerWidth;
        settings.SaveSetting();
    }
}
