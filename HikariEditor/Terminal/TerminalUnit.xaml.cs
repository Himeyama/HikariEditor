using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace HikariEditor;

public sealed partial class TerminalUnit : Page
{
    ConPtySession? _session;
    readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    bool _webReady;

    public TerminalUnit()
    {
        InitializeComponent();

        // ビジュアルツリーに追加された後に WebView2 を初期化する
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        await WebViewTerminal.EnsureCoreWebView2Async();
        WebViewTerminal.CoreWebView2.WebMessageReceived += OnWebMessage;

        string htmlPath = Path.Combine(AppContext.BaseDirectory, "Terminal", "index.html");
        string theme = ActualTheme == ElementTheme.Dark ? "dark" : "light";
        WebViewTerminal.Source = new Uri($"{htmlPath}?theme={theme}");

        // シェルの起動は端末サイズが分かる 'ready' 受信時まで遅延する
    }

    void OnUnloaded(object sender, RoutedEventArgs e)
    {
        try { _session?.Dispose(); }
        catch { /* 解放失敗は無視 */ }
        _session = null;
    }

    void StartShell(short cols, short rows)
    {
        string exe = ResolvePwsh();
        // ConPTY 上ではシェルが対話モードで動くので、プロンプトもエコーもシェル任せ。
        // 余計な起動メッセージだけ抑制する。
        string commandLine = $"\"{exe}\" -NoLogo";
        string workingDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        _session = new ConPtySession();
        _session.Exited += () => QueueWrite("\r\n[プロセスが終了しました]\r\n");

        try
        {
            _session.Start(commandLine, workingDir, cols, rows);
        }
        catch (Exception ex)
        {
            QueueWrite($"\r\nシェルの起動に失敗しました: {ex.Message}\r\n");
            return;
        }

        // 擬似コンソールの出力（ANSI エスケープ込みのバイト列）を読み取って端末へ流す
        new Thread(PumpOutput) { IsBackground = true }.Start();
    }

    static string ResolvePwsh()
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (path != null)
        {
            foreach (string dir in path.Split(Path.PathSeparator))
            {
                try
                {
                    string candidate = Path.Combine(dir, "pwsh.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch { /* 不正な PATH エントリは無視 */ }
            }
        }
        return "powershell.exe";
    }

    void PumpOutput()
    {
        Stream output = _session!.Output;
        byte[] buf = new byte[4096];
        try
        {
            while (true)
            {
                int n = output.Read(buf, 0, buf.Length);
                if (n <= 0) break;
                // UTF-8 のマルチバイト文字がチャンク境界で分割されても
                // xterm.js 側の UTF-8 デコーダが復元できるよう、バイト列を
                // Base64 でそのまま転送する
                string b64 = Convert.ToBase64String(buf, 0, n);
                QueueWrite(b64);
            }
        }
        catch { /* プロセス停止時の読み取りエラーは無視 */ }
    }

    void QueueWrite(string base64)
    {
        if (_webReady)
            _dispatcher.TryEnqueue(() => SendToTerminal(base64));
        // _webReady になる前（=ready 受信前）は出力は発生しないため捨ててよい
    }

    void SendToTerminal(string base64)
    {
        if (WebViewTerminal.CoreWebView2 == null) return;
        string json = JsonSerializer.Serialize(new { type = "write", data = base64 });
        WebViewTerminal.CoreWebView2.PostWebMessageAsJson(json);
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
                    if (_session == null)
                        StartShell(ReadShort(doc, "cols", 80), ReadShort(doc, "rows", 24));
                    break;
                case "input":
                    string? data = doc.RootElement.GetProperty("data").GetString();
                    if (data != null && _session != null)
                    {
                        // 入力は UTF-8 バイト列として擬似コンソールへ書き込む。
                        // エコーもプロンプト再描画もシェル側が行う。
                        byte[] bytes = Encoding.UTF8.GetBytes(data);
                        try
                        {
                            _session.Input.Write(bytes, 0, bytes.Length);
                            _session.Input.Flush();
                        }
                        catch { /* シェル終了後の書き込みは無視 */ }
                    }
                    break;
                case "resize":
                    _session?.Resize(ReadShort(doc, "cols", 80), ReadShort(doc, "rows", 24));
                    break;
            }
        }
        catch { /* 不正な JSON は無視 */ }
    }

    static short ReadShort(JsonDocument doc, string name, short fallback)
    {
        if (doc.RootElement.TryGetProperty(name, out JsonElement el)
            && el.TryGetInt32(out int v) && v is > 0 and <= short.MaxValue)
            return (short)v;
        return fallback;
    }
}
