using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Text.Json;

namespace HikariEditor;

public sealed partial class EditorUnit : UserControl
{
    readonly string _fileName;
    readonly Editor _editor;

    public EditorUnit(string fileName, Editor editor)
    {
        InitializeComponent();
        _fileName = fileName;
        _editor = editor;

        // ビジュアルツリーに追加された後に WebView2 を初期化する
        Loaded += OnLoaded;
    }

    async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (!File.Exists(_fileName)) return;

        // ナビゲーション前に購読しておき、ページ側の 'ready' を取りこぼさない
        await WebView.EnsureCoreWebView2Async();
        WebView.CoreWebView2.WebMessageReceived += OnWebMessage;

        string extension = Path.GetExtension(_fileName);
        string editorDir = $"{Path.GetTempPath()}HikariEditor";
        string uri = $"{editorDir}\\editor\\index.html?extension={extension}";
        if (ActualTheme == ElementTheme.Light)
            uri += "&theme=vs-light";
        WebView.Source = new Uri(uri);
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
                    // Monaco の初期化完了通知。ホストが開いたファイルの内容を返す
                    SendFileContent();
                    break;
                case "save":
                    _editor.OnSave(_fileName, ReadString(doc, "src"));
                    break;
                case "autosave":
                    _editor.OnAutoSave(_fileName, ReadString(doc, "src"));
                    break;
                case "copy":
                    Editor.OnCopy(ReadString(doc, "text"));
                    break;
            }
        }
        catch { /* 不正な JSON は無視 */ }
    }

    void SendFileContent()
    {
        string src;
        try
        {
            src = File.ReadAllText(_fileName);
        }
        catch
        {
            // 読み込み失敗時は空のまま編集できる状態にする
            return;
        }
        string json = JsonSerializer.Serialize(new { type = "load", src });
        WebView.CoreWebView2.PostWebMessageAsJson(json);
    }

    // クリップボードのテキストを Monaco の選択範囲へ挿入する（ホスト → JS）
    public void Paste(string text)
    {
        if (WebView.CoreWebView2 == null) return;
        string json = JsonSerializer.Serialize(new { type = "paste", text });
        WebView.CoreWebView2.PostWebMessageAsJson(json);
    }

    // 選択範囲のコピーを Monaco に要求する（結果は copy メッセージで返る）
    public void RequestCopy()
    {
        if (WebView.CoreWebView2 == null) return;
        string json = JsonSerializer.Serialize(new { type = "copy-request" });
        WebView.CoreWebView2.PostWebMessageAsJson(json);
    }

    static string ReadString(JsonDocument doc, string name)
        => doc.RootElement.TryGetProperty(name, out JsonElement el) ? el.GetString() ?? "" : "";
}
