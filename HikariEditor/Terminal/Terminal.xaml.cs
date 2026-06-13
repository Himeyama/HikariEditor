using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HikariEditor
{
    public sealed partial class Terminal : Page
    {
        MainWindow? mainWindow;

        static public void ClickOpenTerminal(MainWindow mainWindow)
        {
            mainWindow.terminalFrame.Navigate(typeof(Terminal), mainWindow);
            mainWindow.terminalFrame.Height = 300;
            mainWindow.OpenTerminal.IsEnabled = false;

            // Navigate は同期的に OnNavigatedTo を呼ぶのでこの時点で terminal は有効。
            // ターミナルを開いたときだけタブを用意する（ログ単独表示では作らない）。
            if (mainWindow.terminal!.terminalTabs.TabItems.Count == 0)
                mainWindow.terminal.AddNewTab(mainWindow.terminal.terminalTabs);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            mainWindow = (MainWindow)e.Parameter;
            mainWindow.terminal = this;
            base.OnNavigatedTo(e);
        }

        public Terminal()
        {
            InitializeComponent();
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
            mainWindow!.logTabPanel = frame.LogTabPanel;
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
            /* 閉じるタブがログ出力の場合、メニューボタンを有効にし、閉じた状態を保存する */
            if (args.Tab.Content is LogPage)
            {
                mainWindow!.OpenLog.IsEnabled = true;

                // 次回起動時に開いた状態を復元しないよう、閉じたことを保存する
                Settings settings = new();
                settings.LoadSetting();
                settings.LogOpen = false;
                settings.SaveSetting();
            }

            /* 該当するタブを削除 */
            sender.TabItems.Remove(args.Tab);

            /* タブが空になった場合、メニューボタンを有効にする */
            if (sender.TabItems.Count == 0)
            {
                mainWindow!.OpenTerminal.IsEnabled = true;
                mainWindow.OpenLog.IsEnabled = true;
                mainWindow.terminalFrame.Height = 0;
            };
        }
    }
}
