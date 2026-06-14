using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Linq;

namespace HikariEditor;

public sealed partial class Terminal : Page
{
    MainWindow? _mainWindow;

    public Terminal()
    {
        InitializeComponent();
    }

    public static void ClickOpenTerminal(MainWindow mainWindow)
    {
        // すでに Terminal ページが表示されている場合は再ナビゲートしない。
        // 再ナビゲートすると新しいページが生成され、既存のログタブが失われるため。
        if (mainWindow.terminalFrame.Content is not Terminal)
            mainWindow.terminalFrame.Navigate(typeof(Terminal), mainWindow);
        mainWindow.terminalFrame.Height = 300;

        // メニューは常に有効。クリックするたびにターミナルタブを増やす。
        // Navigate は同期的に OnNavigatedTo を呼ぶのでこの時点で terminal は有効。
        mainWindow.terminal!.AddNewTab(mainWindow.terminal.terminalTabs);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _mainWindow = (MainWindow)e.Parameter;
        _mainWindow.terminal = this;
        base.OnNavigatedTo(e);
    }

    public void AddNewLogPage(TabView tabs)
    {
        LogPage frame = new();
        TabViewItem newTab = new()
        {
            Header = "ログ出力",
            Content = frame,
            IsSelected = true
        };
        _mainWindow!.logTabPanel = frame.LogTabPanel;
        tabs.TabItems.Add(newTab);
    }

    // タブの追加
    public void AddNewTab(TabView tabs)
    {
        TerminalUnit frame = new();
        TabViewItem newTab = new()
        {
            Header = "ターミナル",
            Content = frame,
            IsSelected = true
        };
        tabs.TabItems.Add(newTab);
    }

    // タブの追加ボタンクリック
    private void TabViewAddTabButtonClick(TabView sender, object args)
    {
        AddNewTab(sender);
    }

    // ターミナル・ログ出力タブを閉じる
    private void TabViewTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        /* 閉じるタブがログ出力の場合、閉じた状態を保存する */
        if (args.Tab.Content is LogPage)
        {
            // 次回起動時に開いた状態を復元しないよう、閉じたことを保存する
            Settings settings = new();
            settings.LoadSetting();
            settings.LogOpen = false;
            settings.SaveSetting();
        }

        /* 該当するタブを削除 */
        sender.TabItems.Remove(args.Tab);

        /* ログタブが残っていなければ「ログを開く」を再び有効にする。
           「ターミナルを開く」は常に有効（クリックでタブを増やせる）なので触らない。 */
        bool hasLog = sender.TabItems.Any(tab => tab is TabViewItem { Content: LogPage });
        _mainWindow!.OpenLog.IsEnabled = !hasLog;

        /* タブが空になった場合はパネルを畳む */
        if (sender.TabItems.Count == 0)
            _mainWindow.terminalFrame.Height = 0;
    }
}
