// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HikariEditor
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Open : Page
    {
        List<Directories> items;
        ApplicationDataContainer config;
        Frame explorerFrame;
        MainWindow mainWindow;
        string currentDir;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            mainWindow = e.Parameter as MainWindow;
            explorerFrame = mainWindow.contentFrame;
            //mainWindow.contentFrame.Navigate(typeof(Explorer));
            base.OnNavigatedTo(e);
        }

        public Open()
        {
            InitializeComponent();
            config = ApplicationData.Current.LocalSettings;
            DirOpenHome();
        }

        private void Directories_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if ((Directories)Directories.SelectedValue == null)
                return;
            DirOpenParentBtn.IsEnabled = true;
            string dir = ((Directories)Directories.SelectedValue).Path;
            currentDir = dir;
            DirPath.Text = dir;
            string[] dirs = Directory.GetDirectories(dir);
            items = new();
            foreach (string d in dirs)
            {
                items.Add(new Directories { Path = d, Name = Path.GetFileName(d) });
            }
            Directories.ItemsSource = items;
            OpenBtn.IsEnabled = true;
        }

        private void DirOpenHomeBtnClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            DirOpenHome();
        }

        void DirOpenHome()
        {
            DirOpenParentBtn.IsEnabled = true;
            items = new();
            string homeDir = Environment.GetEnvironmentVariable("userprofile");
            currentDir = homeDir;
            string[] homeDirs = Directory.GetDirectories(homeDir);
            foreach (string dir in homeDirs)
            {
                items.Add(new Directories { Path = dir, Name = Path.GetFileName(dir) });
            }
            DirPath.Text = homeDir;
            Directories.ItemsSource = items;
            OpenBtn.IsEnabled = true;
        }

        void DirOpenComputer()
        {
            DirOpenParentBtn.IsEnabled = false;
            items = new();
            foreach (string drive in Directory.GetLogicalDrives())
            {
                items.Add(new Directories { Path = drive, Name = "" });
            }
            DirPath.Text = "";
            currentDir = "";
            Directories.ItemsSource = items;
            OpenBtn.IsEnabled = false;
        }

        void DirOpenComputerClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            DirOpenComputer();
        }

        void DirOpenParentClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            items = new();
            string dir = DirPath.Text;
            if (string.IsNullOrEmpty(dir))
                return;
            DirectoryInfo parentDirInfo = Directory.GetParent(currentDir);
            if (parentDirInfo == null)
            {
                DirOpenComputer();
                return;
            }
            string parentDir = parentDirInfo.FullName;
            string[] parentDirs = Directory.GetDirectories(parentDir);
            foreach (string d in parentDirs)
            {
                items.Add(new Directories { Path = d, Name = Path.GetFileName(d) });
            }
            DirPath.Text = parentDir;
            currentDir = parentDir;
            Directories.ItemsSource = items;
            if (currentDir == "")
                OpenBtn.IsEnabled = false;
        }

        private void OpenBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            //string openDirPath = ((Directories)Directories.SelectedValue).Path;
            string openDirPath = DirPath.Text;
            config.Values["openDirPath"] = openDirPath;
            explorerFrame.Navigate(typeof(Explorer));
            mainWindow.Menu.SelectedItem = mainWindow.ItemExplorer;
            mainWindow.editorFrame.Navigate(typeof(Editor));
        }

        private void Directories_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if ((Directories)Directories.SelectedValue == null)
                return;
            DirPath.Text = ((Directories)Directories.SelectedValue).Path;
        }
    }
}
