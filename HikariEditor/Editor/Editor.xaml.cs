using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace HikariEditor;

public sealed partial class Editor : Page
{
    readonly List<string> _tabPaths = [];

    // テキストタブの改行コード（パス → "LF" / "CRLF"）。タブ切替時にステータスバーへ反映する。
    readonly Dictionary<string, string> _newlines = [];

    // Monaco を使わないタブ（画像/動画/PDF/SVG/バイナリ）のコントロール（パス → コントロール）。
    // ContentHost 上で可視状態を切り替えて表示する。
    readonly Dictionary<string, FrameworkElement> _mediaViews = [];

    // 単一 WebView2（Monaco）の初期化・準備状態。ready 受信前のメッセージは _pending に貯める。
    bool _webInitStarted;
    bool _webReady;
    readonly List<string> _pending = [];

    public List<string> TabPaths => _tabPaths;
    public MainWindow? MainWindow { get; set; }
    public int Counter { get; set; }

    public Editor()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        MainWindow = e.Parameter as MainWindow;
        MainWindow!.editor = this;
        base.OnNavigatedTo(e);
    }

    // テキストタブで使う単一 WebView2 を遅延初期化する（Monaco の素材は起動時に
    // ダウンロード／コピーされるため、最初のテキストファイルを開く時点では揃っている）。
    async void EnsureWebView()
    {
        if (_webInitStarted) return;
        _webInitStarted = true;

        await WebView.EnsureCoreWebView2Async();
        WebView.CoreWebView2.WebMessageReceived += OnWebMessage;

        string editorDir = $"{Path.GetTempPath()}HikariEditor";
        string uri = $"{editorDir}\\editor\\index.html";
        if (ActualTheme == ElementTheme.Light)
            uri += "?theme=vs-light";
        WebView.Source = new Uri(uri);
    }

    // JS（Monaco）へメッセージを送る。ready 前は貯めておき、ready 受信時にまとめて流す。
    void Post(object message)
    {
        string json = JsonSerializer.Serialize(message);
        if (_webReady && WebView.CoreWebView2 is not null)
            WebView.CoreWebView2.PostWebMessageAsJson(json);
        else
            _pending.Add(json);
    }

    void OnWebMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        string raw = args.TryGetWebMessageAsString();
        try
        {
            using JsonDocument doc = JsonDocument.Parse(raw);
            string? type = doc.RootElement.GetProperty("type").GetString();
            switch (type)
            {
                case "ready":
                    _webReady = true;
                    foreach (string json in _pending)
                        WebView.CoreWebView2.PostWebMessageAsJson(json);
                    _pending.Clear();
                    break;
                case "save":
                    OnSave(ReadString(doc, "path"), ReadString(doc, "src"));
                    break;
                case "autosave":
                    OnAutoSave(ReadString(doc, "path"), ReadString(doc, "src"));
                    break;
                case "copy":
                    OnCopy(ReadString(doc, "text"));
                    break;
            }
        }
        catch { /* 不正な JSON は無視 */ }
    }

    // クリップボードのテキストを選択中タブの Monaco に貼り付ける
    public void CallPasteFunction(string text)
    {
        if (Tabs.SelectedItem is not TabViewItem tab) return;
        if (_mediaViews.ContainsKey(tab.Name)) return;   // Monaco を使わないタブは対象外
        Post(new { type = "paste", text });
    }

    // 選択中タブの Monaco に選択範囲のコピーを要求する
    public void CallCopyFunction()
    {
        if (Tabs.SelectedItem is not TabViewItem tab) return;
        if (_mediaViews.ContainsKey(tab.Name)) return;
        Post(new { type = "copy-request" });
    }

    private void TabViewCloseTab(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        CloseTab(args.Tab);
    }

    // 指定したファイルのタブが開いていれば閉じる（エクスプローラーからの削除時に使用）
    public void CloseTabByPath(string fileName)
    {
        if (!TabPaths.Contains(fileName)) return;
        if (Tabs.FindName(fileName) is TabViewItem tab)
            CloseTab(tab);
    }

    private void CloseTab(TabViewItem tab)
    {
        string path = tab.Name;
        TabPaths.Remove(path);
        Tabs.TabItems.Remove(tab);

        if (_mediaViews.TryGetValue(path, out FrameworkElement? view))
        {
            // ビジュアルツリーから外して内蔵 WebView2 などを解放する
            ContentHost.Children.Remove(view);
            _mediaViews.Remove(path);
        }
        else
        {
            // テキストタブは JS 側の model を破棄する
            _newlines.Remove(path);
            Post(new { type = "close", path });
        }

        if (TabPaths.Count == 0)
        {
            MainWindow!.editorFrame.Height = 0;
            MainWindow.previewFrame.Height = 0;
        }
        UpdateContentVisibility();
    }

    // 拡張子に応じて開き方を変える。forceKind を渡すと分類を上書きできる
    // （SVG をテキスト／WebView のどちらで開くか、右クリックメニューから選ぶ用途）。
    internal void OpenFile(FileItem file, FileKind? forceKind = null)
    {
        if (!File.Exists(file.Path)) return;

        // 既に開いている場合、通常クリック（forceKind なし）は選択するだけ。
        // 開き方を指定したときは一旦閉じてから開き直し、別モードで表示できるようにする。
        if (TabPaths.Contains(file.Path) && Tabs.FindName(file.Path) is TabViewItem existing)
        {
            if (forceKind is null)
            {
                existing.IsSelected = true;
                return;
            }
            CloseTab(existing);
        }

        FileKind kind = forceKind ?? FileKinds.Classify(file.Path);
        if (kind == FileKind.Text)
            OpenTextTab(file);
        else
            OpenMediaTab(file, kind);
    }

    // テキストファイルを単一 Monaco の新しい model として開く
    void OpenTextTab(FileItem file)
    {
        EnsureWebView();

        string src;
        try
        {
            src = File.ReadAllText(file.Path);
        }
        catch
        {
            // 読み込み失敗時は開かない
            return;
        }

        // 改行コードを判定して model（パス）ごとに保持する
        _newlines[file.Path] = src.Contains("\r\n") ? "CRLF" : "LF";
        Post(new { type = "open", path = file.Path, src });
        AddTab(file, FileKind.Text);
    }

    // Monaco を使わないファイルを個別コントロールで開く
    void OpenMediaTab(FileItem file, FileKind kind)
    {
        FrameworkElement view = kind == FileKind.Binary
            ? new HexView(file.Path)
            : new MediaUnit(file.Path);   // Image / Video / Pdf / Svg
        view.Visibility = Visibility.Collapsed;
        ContentHost.Children.Add(view);
        _mediaViews[file.Path] = view;
        AddTab(file, kind);
    }

    void AddTab(FileItem file, FileKind kind)
    {
        TabPaths.Add(file.Path);
        TabViewItem newTab = new()
        {
            IconSource = new SymbolIconSource() { Symbol = IconFor(kind) },
            Header = file.Name,
            Content = null,   // 表示は ContentHost に集約する
            Name = file.Path,
            IsSelected = true
        };
        Tabs.TabItems.Add(newTab);
        MainWindow!.editorFrame.Height = double.NaN;
    }

    // 選択中タブに応じて WebView2／個別コントロールの可視状態を切り替える
    void UpdateContentVisibility()
    {
        string? path = (Tabs.SelectedItem as TabViewItem)?.Name;
        bool isText = path is not null && !_mediaViews.ContainsKey(path);
        WebView.Visibility = isText ? Visibility.Visible : Visibility.Collapsed;
        foreach ((string key, FrameworkElement view) in _mediaViews)
            view.Visibility = key == path ? Visibility.Visible : Visibility.Collapsed;
    }

    static Symbol IconFor(FileKind kind) => kind switch
    {
        FileKind.Image or FileKind.Svg => Symbol.Pictures,
        FileKind.Video => Symbol.Video,
        FileKind.Pdf => Symbol.Page2,
        FileKind.Binary => Symbol.Calculator,
        _ => Symbol.Document
    };

    // Monaco からの保存メッセージを処理する
    public void OnSave(string fileName, string src)
    {
        string newline = _newlines.TryGetValue(fileName, out string? nl) ? nl : "LF";
        FileItem fileItem = new(fileName);
        fileItem.Save(src, newline);
        MainWindow!.StatusBar.Text = $"{fileItem.Name} を保存しました。";
        LogPage.AddLog(MainWindow, $"{fileItem.Name} を保存しました。");
        Counter++;
        DelayResetStatusBar(1000);
        if (fileItem.Extension == ".tex")
        {
            MainWindow.StatusBar.Text = $"{fileItem.Name} を保存しました。TeX のコンパイルを実行しています...";
            LogPage.AddLog(MainWindow, "LaTeX のコンパイルを実行しています...");
            Counter++;
            DelayResetStatusBar(1000);
            _ = LaTeX.Compile(MainWindow, fileItem, this);
        }
    }

    // Monaco からの自動保存メッセージを処理する
    public void OnAutoSave(string fileName, string src)
    {
        if (!MainWindow!.AutoSave.IsChecked)
            return;
        string newline = _newlines.TryGetValue(fileName, out string? nl) ? nl : "LF";
        FileItem fileItem = new(fileName);
        fileItem.Save(src, newline);
        MainWindow.StatusBar.Text = $"{fileItem.Name} を自動保存しました。";
        LogPage.AddLog(MainWindow, $"{fileItem.Name} を自動保存しました。");
        Counter++;
        DelayResetStatusBar(1000);
    }

    // Monaco から届いた選択テキストをクリップボードへ書き込む
    public static void OnCopy(string text)
    {
        DataPackage dataPackage = new();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
    }

    public async void DelayResetStatusBar(int sec)
    {
        int count = Counter;
        await Task.Delay(TimeSpan.FromMilliseconds(sec));
        if (count >= Counter)
        {
            MainWindow!.StatusBar.Text = "";
        }
    }

    private void EditorTabChange(object sender, SelectionChangedEventArgs e)
    {
        UpdateContentVisibility();
        if (((TabView)sender).SelectedItem is not TabViewItem selectedItem) return;
        string path = selectedItem.Name;
        string extension = Path.GetExtension(path);
        MainWindow!.rightArea.ColumnDefinitions[1].Width = extension == ".tex"
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        // テキストタブなら表示する model を切り替え、改行コードをステータスバーへ反映する
        if (!_mediaViews.ContainsKey(path))
        {
            Post(new { type = "switch", path });
            if (_newlines.TryGetValue(path, out string? nl))
                MainWindow.NLBtn.Content = nl;
        }
    }

    // ステータスバーの LF/CRLF ボタンが切り替えられたとき、選択中タブへ適用して保存する
    public void ApplyNewline(string newline)
    {
        if (Tabs.SelectedItem is not TabViewItem tab) return;
        if (_mediaViews.ContainsKey(tab.Name)) return;
        _newlines[tab.Name] = newline;
        // 現在の内容を save で返してもらい、新しい改行コードで書き込み直す
        Post(new { type = "save-request", path = tab.Name });
    }

    static string ReadString(JsonDocument doc, string name)
        => doc.RootElement.TryGetProperty(name, out JsonElement el) ? el.GetString() ?? "" : "";
}
