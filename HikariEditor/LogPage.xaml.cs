using Microsoft.UI.Xaml.Controls;
using System;

namespace HikariEditor
{
    public sealed partial class LogPage : Page
    {
        //MainWindow mainWindow;
        static public void ClickOpenLog(MainWindow mainWindow)
        {
            mainWindow.terminalFrame.Navigate(typeof(Terminal), mainWindow);
            mainWindow.terminalFrame.Height = 300;
            mainWindow.OpenLog.IsEnabled = false;
            mainWindow.terminal.AddNewLogPage(mainWindow.terminal.terminalTabs);
        }
        static public void AddLog(MainWindow mainWindow, string text)
        {
            if (mainWindow.logTabPanel != null)
            {
                ListViewItem listViewItem = new();
                TextBlock block = new();
                DateTime now = DateTime.Now;
                block.Text = $"{now} {text}";
                listViewItem.Content = block;
                mainWindow.logTabPanel.Children.Add(block);
                ScrollViewer scrollViewer = (ScrollViewer)mainWindow.logTabPanel.Parent;
                double maxVerticalOffset = scrollViewer.ScrollableHeight + 16;
                scrollViewer.ScrollToVerticalOffset(maxVerticalOffset);
            }
        }

        public LogPage()
        {
            InitializeComponent();
        }

        // ƒ^ƒu‚Ì’Ç‰Á
    }
}
