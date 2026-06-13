using System;
using Microsoft.UI.Xaml.Controls;

namespace HikariEditor
{
    public sealed partial class LogPage : Page
    {
        static public void ClickOpenLog(MainWindow mainWindow)
        {
            mainWindow.terminalFrame.Navigate(typeof(Terminal), mainWindow);
            mainWindow.terminalFrame.Height = 300;
            mainWindow.OpenLog.IsEnabled = false;
            mainWindow.terminal!.AddNewLogPage(mainWindow.terminal.terminalTabs);

            // ログ表示状態を記憶し、次回起動時に開いた状態を復元する
            Settings settings = new();
            settings.LoadSetting();
            settings.LogOpen = true;
            settings.SaveSetting();
        }

        static public void AddLog(MainWindow mainWindow, string text)
        {
            if (mainWindow.logTabPanel != null)
            {
                TextBlock block = new();
                DateTime now = DateTime.Now;
                block.Text = $"{now} {text}";
                mainWindow.logTabPanel.Children.Add(block);

                if (mainWindow.logTabPanel.Parent is ScrollViewer scrollViewer)
                {
                    double maxVerticalOffset = scrollViewer.ScrollableHeight + 16;
                    scrollViewer.ScrollToVerticalOffset(maxVerticalOffset);
                }
            }
        }

        public LogPage()
        {
            InitializeComponent();
        }
    }
}
