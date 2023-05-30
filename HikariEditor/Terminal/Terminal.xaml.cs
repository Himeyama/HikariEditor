using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HikariEditor
{
    public sealed partial class Terminal : Page
    {
        MainWindow mainWindow;

        static public void ClickOpenTerminal(MainWindow mainWindow)
        {
            mainWindow.terminalFrame.Navigate(typeof(Terminal), mainWindow);
            mainWindow.terminalFrame.Height = 300;
            mainWindow.OpenTerminal.IsEnabled = false;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            mainWindow = e.Parameter as MainWindow;
            base.OnNavigatedTo(e);
        }

        public Terminal()
        {
            InitializeComponent();
            AddNewTab(terminalTabs);
        }

        // タブの追加
        void AddNewTab(TabView tabs)
        {
            TabViewItem newTab = new();
            newTab.Header = "Terminal";
            TerminalUnit frame = new();
            newTab.Content = frame;
            newTab.IsSelected = true;
            tabs.TabItems.Add(newTab);
        }

        // タブの追加ボタンをクリック
        private void TabViewAddTabButtonClick(TabView sender, object args)
        {
            AddNewTab(sender);
        }

        // タブを閉じる
        private void TabViewTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            sender.TabItems.Remove(args.Tab);
            if (sender.TabItems.Count == 0)
            {
                mainWindow.OpenTerminal.IsEnabled = true;
                mainWindow.terminalFrame.Height = 0;
            };
        }
    }
}
