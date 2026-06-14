using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace HikariEditor;

public sealed partial class Open : Page
{
    List<Directories>? _items;
    Frame? _explorerFrame;
    MainWindow? _mainWindow;
    string? _currentDir;

    public Open()
    {
        InitializeComponent();
        DirOpenHome();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            _explorerFrame = mainWindow.contentFrame;
        }

        base.OnNavigatedTo(e);
    }

    private void Directories_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((Directories)Directories.SelectedValue == null)
            return;
        DirOpenParentBtn.IsEnabled = true;
        string? dir = ((Directories)Directories.SelectedValue).Path;
        _currentDir = dir;
        DirPath.Text = dir;
        _items = [];
        foreach (string d in Directory.GetDirectories(dir!))
        {
            _items.Add(new Directories { Path = d, Name = Path.GetFileName(d) });
        }
        Directories.ItemsSource = _items;
        OpenBtn.IsEnabled = true;
    }

    private void DirOpenHomeBtnClick(object sender, RoutedEventArgs e)
    {
        DirOpenHome();
    }

    void DirOpenHome()
    {
        DirOpenParentBtn.IsEnabled = true;
        _items = [];
        string? homeDir = Environment.GetEnvironmentVariable("userprofile");
        _currentDir = homeDir;
        if (homeDir == null)
        {
            Error.Dialog("環境変数未定義エラー", "環境変数が未定義です。", _mainWindow!.Content.XamlRoot);
            return;
        }
        foreach (string dir in Directory.GetDirectories(homeDir))
        {
            _items.Add(new Directories { Path = dir, Name = Path.GetFileName(dir) });
        }
        DirPath.Text = homeDir;
        Directories.ItemsSource = _items;
        OpenBtn.IsEnabled = true;
    }

    void DirOpenComputer()
    {
        DirOpenParentBtn.IsEnabled = false;
        _items = [];
        foreach (string drive in Directory.GetLogicalDrives())
        {
            _items.Add(new Directories { Path = drive, Name = drive });
        }
        DirPath.Text = "";
        _currentDir = "";
        Directories.ItemsSource = _items;
        OpenBtn.IsEnabled = true;
    }

    void DirOpenComputerClick(object sender, RoutedEventArgs e)
    {
        DirOpenComputer();
    }

    void DirOpenParentClick(object sender, RoutedEventArgs e)
    {
        _items = [];
        string dir = DirPath.Text;
        if (string.IsNullOrEmpty(dir))
            return;
        if (_currentDir == null)
        {
            Error.Dialog("変数未定義エラー", "現在のディレクトリが未定義です。", _mainWindow!.Content.XamlRoot);
            return;
        }
        DirectoryInfo? parentDirInfo = Directory.GetParent(_currentDir);
        if (parentDirInfo == null)
        {
            DirOpenComputer();
            return;
        }
        string parentDir = parentDirInfo.FullName;
        foreach (string d in Directory.GetDirectories(parentDir))
        {
            _items.Add(new Directories { Path = d, Name = Path.GetFileName(d) });
        }
        DirPath.Text = parentDir;
        _currentDir = parentDir;
        Directories.ItemsSource = _items;
        if (_currentDir == "")
            OpenBtn.IsEnabled = false;
    }

    // 開くボタンのクリック
    private void OpenBtn_Click(object sender, RoutedEventArgs e)
    {
        // 既存設定を読み込んでから更新する。LoadSetting を挟まないと
        // LogOpen や AutoSave などが既定値で丸ごと上書きされてしまう。
        Settings settings = new();
        settings.LoadSetting();
        settings.OpenDirPath = DirPath.Text;
        settings.SaveSetting();
        _explorerFrame!.Navigate(typeof(Explorer), _mainWindow);
        _mainWindow!.Menu.SelectedItem = _mainWindow.ItemExplorer;
        _mainWindow.editorFrame.Navigate(typeof(Editor), _mainWindow);
        _mainWindow.OpenExplorer.IsEnabled = true;
        _mainWindow.SideMenuEditorArea.ColumnDefinitions[0].Width = new GridLength(360);
    }

    private void Directories_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if ((Directories)Directories.SelectedValue == null)
            return;
        DirPath.Text = ((Directories)Directories.SelectedValue).Path;
    }

    private void OpenCloseButtonClick(object sender, RoutedEventArgs e)
    {
        _mainWindow!.editorFrame.Navigate(typeof(Editor), _mainWindow);
    }
}
