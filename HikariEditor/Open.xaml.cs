// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.IO;

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

        public Open()
        {
            InitializeComponent();

            DirOpenHome();
        }

        private void Directories_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            DirOpenParentBtn.IsEnabled = true;
            string dir = ((Directories)Directories.SelectedValue).Path;
            DirPath.Text = dir;
            string[] dirs = Directory.GetDirectories(dir);
            items = new();
            foreach (string d in dirs)
            {
                items.Add(new Directories { Path = d });
            }
            Directories.ItemsSource = items;
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
            string[] homeDirs = Directory.GetDirectories(homeDir);
            foreach (string dir in homeDirs)
            {
                items.Add(new Directories { Path = dir });
            }
            DirPath.Text = homeDir;
            Directories.ItemsSource = items;
        }

        void DirOpenComputer()
        {
            DirOpenParentBtn.IsEnabled = false;
            items = new();
            foreach (string drive in Directory.GetLogicalDrives())
            {
                items.Add(new Directories { Path = drive });
            }
            DirPath.Text = "";
            Directories.ItemsSource = items;
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
            DirectoryInfo parentDirInfo = Directory.GetParent(dir);
            if (parentDirInfo == null)
            {
                DirOpenComputer();
                return;
            }
            string parentDir = parentDirInfo.FullName;
            string[] parentDirs = Directory.GetDirectories(parentDir);
            foreach (string d in parentDirs)
            {
                items.Add(new Directories { Path = d });
            }
            DirPath.Text = parentDir;
            Directories.ItemsSource = items;
        }
    }
}
