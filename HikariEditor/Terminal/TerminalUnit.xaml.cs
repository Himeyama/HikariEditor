using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace HikariEditor
{
    public sealed partial class TerminalUnit : Page
    {
        Process? shell;
        readonly DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();
        readonly StringBuilder pendingWrites = new();
        readonly object pendingLock = new();
        bool webReady;

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

            StartShell();
        }

        void OnUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (shell is { HasExited: false })
                    shell.Kill(entireProcessTree: true);
            }
            catch { /* プロセス停止失敗は無視 */ }
            shell?.Dispose();
            shell = null;
        }

        void StartShell()
        {
            string exe = ResolvePwsh();

            // 標準入出力をリダイレクトするとプロンプトが出ない非対話モードに入る。
            // -File - は stdin を 1 本のスクリプトとして読みきった時点で終了してしまうので、
            // stdin の各行を逐次コマンドとして評価し続ける -Command - を使う。
            ProcessStartInfo psi = new()
            {
                FileName = exe,
                Arguments = "-NoLogo -NoProfile -Command -",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            };
            psi.EnvironmentVariables["TERM"] = "xterm-256color";

            shell = new Process { StartInfo = psi, EnableRaisingEvents = true };
            shell.Exited += (_, _) => QueueWrite("\r\n[プロセスが終了しました]\r\n");

            try
            {
                shell.Start();
            }
            catch (Exception ex)
            {
                QueueWrite($"\r\nPowerShell の起動に失敗しました: {ex.Message}\r\n");
                return;
            }

            shell.StandardInput.AutoFlush = true;

            // 端末風に振る舞わせる初期化スクリプトを stdin に流し込む
            string init =
                "$OutputEncoding = [System.Text.Encoding]::UTF8\n" +
                "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8\n" +
                "function prompt { 'PS ' + (Get-Location).Path + '> ' }\n" +
                "Write-Host ('PowerShell ' + $PSVersionTable.PSVersion)\n" +
                "Write-Host (prompt) -NoNewline\n";
            shell.StandardInput.Write(init);

            // 標準出力・標準エラーを別スレッドで読み取って端末へ流す
            new Thread(() => PumpStream(shell.StandardOutput)) { IsBackground = true }.Start();
            new Thread(() => PumpStream(shell.StandardError)) { IsBackground = true }.Start();
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

        void PumpStream(StreamReader reader)
        {
            char[] buf = new char[4096];
            try
            {
                while (!reader.EndOfStream)
                {
                    int n = reader.Read(buf, 0, buf.Length);
                    if (n <= 0) break;
                    string chunk = NormalizeCrlf(new string(buf, 0, n));
                    QueueWrite(chunk);
                }
            }
            catch { /* プロセス停止時の読み取りエラーは無視 */ }
        }

        static string NormalizeCrlf(string s)
        {
            StringBuilder sb = new(s.Length + 8);
            char prev = '\0';
            foreach (char c in s)
            {
                if (c == '\n' && prev != '\r') sb.Append('\r');
                sb.Append(c);
                prev = c;
            }
            return sb.ToString();
        }

        void QueueWrite(string text)
        {
            if (webReady)
                dispatcher.TryEnqueue(() => SendToTerminal(text));
            else
                lock (pendingLock) pendingWrites.Append(text);
        }

        void SendToTerminal(string text)
        {
            if (WebViewTerminal.CoreWebView2 == null) return;
            string json = JsonSerializer.Serialize(new { type = "write", data = text });
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
                        webReady = true;
                        string flushed;
                        lock (pendingLock)
                        {
                            flushed = pendingWrites.ToString();
                            pendingWrites.Clear();
                        }
                        if (flushed.Length > 0) SendToTerminal(flushed);
                        break;
                    case "input":
                        string? data = doc.RootElement.GetProperty("data").GetString();
                        if (data != null && shell is { HasExited: false })
                        {
                            // PowerShell は非対話モードで自前エコーしないので、こちらで端末側に返す
                            SendToTerminal(EchoFor(data));
                            // 改行は LF に正規化して stdin へ
                            string stdinText = data.Replace("\r\n", "\n").Replace("\r", "\n");
                            shell.StandardInput.Write(stdinText);
                            // 改行が含まれていたら次のプロンプトを描画する
                            if (stdinText.Contains('\n'))
                                shell.StandardInput.Write("Write-Host (prompt) -NoNewline\n");
                        }
                        break;
                    case "resize":
                        // 標準入出力モードでは ConPTY のような resize は無く、無視
                        break;
                }
            }
            catch { /* 不正な JSON は無視 */ }
        }

        static string EchoFor(string data)
        {
            // Enter は CRLF にして次行へ。Backspace は破壊的に左へ。Ctrl+C は ^C 表示
            StringBuilder sb = new(data.Length);
            foreach (char c in data)
            {
                if (c == '\r' || c == '\n') sb.Append("\r\n");
                else if (c == '\b' || c == '') sb.Append("\b \b");
                else if (c == '') sb.Append("^C\r\n");
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
