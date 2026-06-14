using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VirtualKey = Windows.System.VirtualKey;

namespace HikariEditor;

public sealed partial class AIPanel : UserControl
{
    public ObservableCollection<ChatMessage> Messages { get; } = [];

    readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();

    ChatClient? _client;
    string? _clientModelId;          // _client がどのモデル設定で作られたか
    ChatMessage? _streaming;         // ストリーミング中のアシスタント発言（断片の追記先）
    CancellationTokenSource? _cts;   // 生成中のリクエストを停止ボタンで中断するため
    bool _busy;

    public AIPanel()
    {
        InitializeComponent();
        messageList.ItemsSource = Messages;

        // AcceptsReturn の TextBox は Enter を自前で処理（改行挿入）して
        // ルーティングを止めるため、XAML の KeyDown では Enter を拾えない。
        // handledEventsToo=true で登録し、処理済みでも確実に受け取る。
        inputBox.AddHandler(KeyDownEvent, new KeyEventHandler(InputKeyDown), handledEventsToo: true);
    }

    // モデル設定が変わったら次回送信で作り直す。会話履歴（クライアント内）はリセットされる。
    public void OnActiveModelChanged()
    {
        _client = null;
        _clientModelId = null;
    }

    void InputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Enter で送信、Shift+Enter で改行
        if (e.Key != VirtualKey.Enter) return;
        bool shift = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
                      & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        if (shift) return;
        e.Handled = true;
        _ = SendAsync();   // 生成中（_busy）なら SendAsync 側で無視される
    }

    // 生成中はボタンが停止として働き、それ以外は送信する。
    void SendClick(object sender, RoutedEventArgs e)
    {
        if (_busy)
            _cts?.Cancel();
        else
            _ = SendAsync();
    }

    async Task SendAsync()
    {
        if (_busy) return;
        string text = inputBox.Text.Trim();
        if (text.Length == 0) return;

        if (!EnsureClient())
        {
            Messages.Add(new ChatMessage(ChatRole.Assistant,
                "LLM が未登録です。右下のボタンからモデルを登録してください。"));
            ScrollToBottom();
            return;
        }

        Messages.Add(new ChatMessage(ChatRole.User, text));
        inputBox.Text = "";
        ScrollToBottom();

        SetBusy(true);
        _cts = new CancellationTokenSource();
        try
        {
            await _client!.SendAsync(text, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            // 停止ボタンによる中断。途中までの出力はそのまま残す。
            _streaming = null;
        }
        catch (Exception ex)
        {
            _streaming = null;
            Messages.Add(new ChatMessage(ChatRole.Tool, $"エラー: {ex.Message}"));
        }
        finally
        {
            _streaming = null;
            _cts?.Dispose();
            _cts = null;
            SetBusy(false);
            ScrollToBottom();
        }
    }

    // アクティブモデルに対応するクライアントを用意する。未登録なら false。
    bool EnsureClient()
    {
        AIConfig config = AIConfig.Load();
        ModelConfig? active = config.ActiveModel;
        if (active is null)
            return false;

        if (_client is null || _clientModelId != active.Id)
        {
            _client = new ChatClient(active, ResolveWorkingDir())
            {
                // コールバックは背景の継続から呼ばれ得るため UI スレッドへ載せ替える
                OnText = chunk => _dispatcher.TryEnqueue(() => AppendAssistantText(chunk)),
                OnTool = label => _dispatcher.TryEnqueue(() => AddToolMessage(label))
            };
            _clientModelId = active.Id;
        }
        return true;
    }

    void AppendAssistantText(string chunk)
    {
        // 直前がツール行などで途切れていれば新しい吹き出しを起こす
        if (_streaming is null)
        {
            _streaming = new ChatMessage(ChatRole.Assistant);
            Messages.Add(_streaming);
        }
        _streaming.Text += chunk;
        ScrollToBottom();
    }

    void AddToolMessage(string label)
    {
        // ツール実行を挟んだら現在の吹き出しは確定。次のテキストは新しい吹き出しへ。
        _streaming = null;
        Messages.Add(new ChatMessage(ChatRole.Tool, label));
        ScrollToBottom();
    }

    // 生成中は入力欄を有効のまま残し、送信ボタンを停止ボタンに切り替える。
    void SetBusy(bool busy)
    {
        _busy = busy;
        sendButton.Content = busy ? StopIcon() : SendIcon();
        ToolTipService.SetToolTip(sendButton, busy ? "停止" : "送信");
    }

    static FontIcon SendIcon() =>
        new() { Glyph = "", FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 14 };

    // 停止は塗りつぶしの四角。アクセントボタン上で見えるよう前景色を使う。
    static Rectangle StopIcon() =>
        new()
        {
            Width = 11,
            Height = 11,
            RadiusX = 2,
            RadiusY = 2,
            Fill = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"]
        };

    // レイアウト確定後に最下部へスクロールする。
    void ScrollToBottom() =>
        _dispatcher.TryEnqueue(DispatcherQueuePriority.Low,
            () => conversationScroll.ChangeView(null, conversationScroll.ScrollableHeight, null));

    // 作業ディレクトリはエクスプローラーで開いているフォルダに合わせる（ターミナルと同じ規則）。
    static string ResolveWorkingDir()
    {
        Settings settings = new();
        settings.LoadSetting();
        if (settings.OpenDirPath != string.Empty && Directory.Exists(settings.OpenDirPath))
            return settings.OpenDirPath;
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
