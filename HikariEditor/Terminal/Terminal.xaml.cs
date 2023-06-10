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

        // タブの追加ボタンをクリック
        private void TabViewAddTabButtonClick(TabView sender, object args)
        {
            AddNewTab(sender);
        }

        // ターミナル・ログ出力タブを閉じる
        private void TabViewTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            /* ログ出力を閉じた場合に、メニューボタンを有効にする */
            if (((TabViewItem)sender.SelectedItem).Content.ToString() == "HikariEditor.LogPage")
                mainWindow!.OpenLog.IsEnabled = true;

            /* 該当するタブを削除 */
            sender.TabItems.Remove(args.Tab);

            /* タブが無くなった場合に、メニューボタンを有効にする */
            if (sender.TabItems.Count == 0)
            {
                mainWindow!.OpenTerminal.IsEnabled = true;
                mainWindow.OpenLog.IsEnabled = true;
                mainWindow.terminalFrame.Height = 0;
            };
        }
    }
}
