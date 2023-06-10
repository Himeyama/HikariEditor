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
                Header = "���O�o��",
                Content = frame,
                IsSelected = true
            };
            mainWindow!.logTabPanel = frame.LogTabPanel;
            tabs.TabItems.Add(newTab);
        }

        // �^�u�̒ǉ�
        public void AddNewTab(TabView tabs)
        {
            TerminalUnit frame = new();
            TabViewItem newTab = new()
            {
                Header = "�^�[�~�i��",
                Content = frame,
                IsSelected = true
            };
            tabs.TabItems.Add(newTab);
        }

        // �^�u�̒ǉ��{�^�����N���b�N
        private void TabViewAddTabButtonClick(TabView sender, object args)
        {
            AddNewTab(sender);
        }

        // �^�[�~�i���E���O�o�̓^�u�����
        private void TabViewTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            /* ���O�o�͂�����ꍇ�ɁA���j���[�{�^����L���ɂ��� */
            if (((TabViewItem)sender.SelectedItem).Content.ToString() == "HikariEditor.LogPage")
                mainWindow!.OpenLog.IsEnabled = true;

            /* �Y������^�u���폜 */
            sender.TabItems.Remove(args.Tab);

            /* �^�u�������Ȃ����ꍇ�ɁA���j���[�{�^����L���ɂ��� */
            if (sender.TabItems.Count == 0)
            {
                mainWindow!.OpenTerminal.IsEnabled = true;
                mainWindow.OpenLog.IsEnabled = true;
                mainWindow.terminalFrame.Height = 0;
            };
        }
    }
}
