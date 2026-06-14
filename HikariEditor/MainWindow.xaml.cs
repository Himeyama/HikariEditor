using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
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
    double aiPanelWidth = 400;

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

        /* git リポジトリならブランチ名ボタンを表示 */
        _ = UpdateBranchButtonAsync();

        /* AI パネルのモデルボタン表示を初期化 */
        UpdateModelButton();
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
        aiPanelWidth = settings.AiPanelWidth;
        bool restoreLog = settings.LogOpen;
        bool restoreAiPanel = settings.AiPanelOpen;

        // ビジュアルツリー構築後に一度だけ復元処理を行う。
        // コンストラクタ段階ではビジュアルツリーが未構築で Frame の
        // Navigate / サイズ変更が反映されないため、ウィンドウ表示後に実行する。
        void Restore(object sender, WindowActivatedEventArgs e)
        {
            Activated -= Restore;
            ShowExplorerPanel();              // エクスプローラーは既定で開いた状態にする
            if (restoreLog) LogPage.ClickOpenLog(this);
            if (restoreAiPanel) ShowAIPanel();
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

    static string OpenDir()
    {
        Settings settings = new();
        settings.LoadSetting();
        return settings.OpenDirPath;
    }

    // 開いているディレクトリが git リポジトリならブランチ名ボタンを表示する。
    // ディレクトリを開き直したときや起動時に呼ぶ。
    public async Task UpdateBranchButtonAsync()
    {
        string? branch = await Git.CurrentBranchAsync(OpenDir());
        if (branch is null)
        {
            BranchBtn.Visibility = Visibility.Collapsed;
        }
        else
        {
            BranchBtn.Content = branch;
            BranchBtn.Visibility = Visibility.Visible;
        }
    }

    private async void ClickBranchBtn(object sender, RoutedEventArgs e)
    {
        string openDir = OpenDir();
        string? current = await Git.CurrentBranchAsync(openDir);
        List<string> branches = await Git.BranchesAsync(openDir);

        ListView listView = new()
        {
            ItemsSource = branches,
            SelectionMode = ListViewSelectionMode.Single,
        };
        if (current is not null)
            listView.SelectedItem = current;

        ContentDialog dialog = new()
        {
            Title = "ブランチを切り替え",
            Content = listView,
            PrimaryButtonText = "切り替え",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;
        if (listView.SelectedItem is not string target || target == current)
            return;

        (bool ok, string message) = await Git.CheckoutAsync(openDir, target);
        if (ok)
            await UpdateBranchButtonAsync();
        else
            Error.Dialog("ブランチ切り替えエラー", message, Content.XamlRoot);
    }

    private void MenuChanged(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        // AI 項目は SelectsOnInvoked=False のため SelectedItem に出ない。
        // 実際にクリックされた項目で分岐する。
        if (args.InvokedItemContainer is not NavigationViewItem item) return;
        if ((string)item.Tag == "Explorer")
        {
            // 開いている（幅がコンパクトレールの 48 より大きい）ときは畳む
            if (SideMenuEditorArea.ColumnDefinitions[0].Width.Value > 48)
                HideExplorerPanel();
            else
                ShowExplorerPanel();
        }
        else if ((string)item.Tag == "AI")
        {
            ToggleAIPanel();
        }
        else if ((string)item.Tag == "Search")
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

    bool AIPanelVisible => SideMenuEditorArea.ColumnDefinitions[4].Width.Value > 0;

    // 「表示」メニュー・サイドの AI 項目・ショートカットから共通で呼ぶトグル。
    public void ToggleAIPanel()
    {
        if (AIPanelVisible)
            HideAIPanel();
        else
            ShowAIPanel();
    }

    public void ShowAIPanel()
    {
        SideMenuEditorArea.ColumnDefinitions[4].Width = new GridLength(aiPanelWidth);
        aiSplitter.Visibility = Visibility.Visible;
        ToggleAI.IsChecked = true;
        ModelBtn.Visibility = Visibility.Visible;
        UpdateModelButton();
        SaveAiPanelOpen(true);
    }

    public void HideAIPanel()
    {
        SideMenuEditorArea.ColumnDefinitions[4].Width = new GridLength(0);
        aiSplitter.Visibility = Visibility.Collapsed;
        ToggleAI.IsChecked = false;
        ModelBtn.Visibility = Visibility.Collapsed;
        SaveAiPanelOpen(false);
    }

    // AI パネルの表示状態を次回起動時の復元用に保存する。
    static void SaveAiPanelOpen(bool open)
    {
        Settings settings = new();
        settings.LoadSetting();
        settings.AiPanelOpen = open;
        settings.SaveSetting();
    }

    void ClickToggleAI(object sender, RoutedEventArgs e) => ToggleAIPanel();

    // ステータスバーのモデルボタンに使用中モデル名を出す。未登録なら案内文。
    public void UpdateModelButton()
    {
        ModelConfig? active = AIConfig.Load().ActiveModel;
        ModelBtn.Content = active is null || string.IsNullOrWhiteSpace(active.Name)
            ? (active is null ? "LLM が未登録" : active.Model)
            : active.Name;
    }

    // モデル設定モーダル。登録・編集・使用モデルの選択を行う。
    private async void ClickModelBtn(object sender, RoutedEventArgs e)
    {
        ModelSettings settings = new();
        ContentDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot,
            Title = "モデル設定",
            Content = settings,
            PrimaryButtonText = "保存",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        settings.Persist();
        UpdateModelButton();
        aiPanel.OnActiveModelChanged();   // 次回送信で新しい設定のクライアントを作り直す
    }

    // ハンドルのドラッグで AI パネルの横幅をリサイズする。パネルは右端にあるので
    // 左へ動かす（delta が負）と広がるよう符号を反転する。
    bool resizingAI;
    double aiResizeStartX;
    double aiResizeStartWidth;

    void AISplitter_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        resizingAI = true;
        aiResizeStartX = e.GetCurrentPoint(SideMenuEditorArea).Position.X;
        aiResizeStartWidth = aiPanelWidth;
        aiSplitter.CapturePointer(e.Pointer);
    }

    void AISplitter_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!resizingAI) return;

        double x = e.GetCurrentPoint(SideMenuEditorArea).Position.X;
        double newWidth = aiResizeStartWidth - (x - aiResizeStartX);

        // エディタ領域を最低限残しつつ、AI パネルが潰れないようにクランプする
        double max = Math.Max(200, SideMenuEditorArea.ActualWidth - 200);
        aiPanelWidth = Math.Clamp(newWidth, 200, max);
        SideMenuEditorArea.ColumnDefinitions[4].Width = new GridLength(aiPanelWidth);
    }

    void AISplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!resizingAI) return;
        resizingAI = false;
        aiSplitter.ReleasePointerCapture(e.Pointer);
        SaveLayout();
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
        settings.AiPanelWidth = aiPanelWidth;
        settings.SaveSetting();
    }
}
