using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;

namespace HikariEditor;

public sealed partial class Explorer : Page
{
    string _fullFile;
    MainWindow? _mainWindow;

    // 直近に右クリックされたツリー項目。コンテキストメニューの各操作が対象にする。
    FileItem? _contextItem;

    public Explorer()
    {
        InitializeComponent();

        Settings settings = new();
        settings.LoadSetting();

        if (settings.OpenDirPath == string.Empty)
        {
            _fullFile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            settings.ExplorerDir = _fullFile;
        }
        else
        {
            _fullFile = settings.OpenDirPath;
        }
        settings.SaveSetting();

        AddTreeViewFiles(_fullFile);
        ExplorerTree.ItemInvoked += FileClick;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _mainWindow = (MainWindow)e.Parameter;
        base.OnNavigatedTo(e);
    }

    // ツリーを選択したとき
    void FileClick(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is not FileItem file) return;
        Settings settings = new();
        settings.LoadSetting();
        if (Directory.Exists(file.Path))
        {
            settings.ExplorerDir = file.Path;
            return;
        }
        else if (File.Exists(file.Path))
        {
            settings.ExplorerDir = Path.GetDirectoryName(file.Path)!;
        }
        settings.SaveSetting();
        _mainWindow!.editor!.OpenFile(file);

        _mainWindow.rightArea.ColumnDefinitions[1].Width =
            file.Extension == ".tex" ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    // 右クリックされた項目を記録しておく（ContextFlyout が開く前に発火する）
    void OnItemRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        _contextItem = (sender as FrameworkElement)?.DataContext as FileItem;
    }

    // エクスプローラーでファイルの場所を開き、その項目を選択状態にする
    void ClickOpenLocation(object sender, RoutedEventArgs e)
    {
        if (_contextItem is not FileItem item) return;
        Process.Start("explorer.exe", $"/select,\"{item.Path}\"");
    }

    void ClickCopyAbsolutePath(object sender, RoutedEventArgs e)
    {
        if (_contextItem is FileItem item) CopyToClipboard(item.Path);
    }

    void ClickCopyRelativePath(object sender, RoutedEventArgs e)
    {
        if (_contextItem is FileItem item)
            CopyToClipboard(Path.GetRelativePath(_fullFile, item.Path));
    }

    static void CopyToClipboard(string text)
    {
        DataPackage dataPackage = new();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
    }

    async void ClickRename(object sender, RoutedEventArgs e)
    {
        if (_contextItem is not FileItem item) return;

        Rename content = new();
        content.newName.Text = item.Name;
        ContentDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot,
            Title = "名前の変更",
            PrimaryButtonText = "OK",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary,
            Content = content
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        string newName = content.newName.Text.Trim();
        if (newName == string.Empty || newName == item.Name) return;

        string newPath = Path.Combine(item.Dirname, newName);
        bool isDirectory = Directory.Exists(item.Path);
        try
        {
            if (isDirectory)
                Directory.Move(item.Path, newPath);
            else
                File.Move(item.Path, newPath);
        }
        catch (IOException err)
        {
            Error.Dialog("名前の変更に失敗しました", err.Message, Content.XamlRoot);
            return;
        }

        // 名前変更前のパスで開いていたタブは閉じる（パスが変わり追従できないため）
        _mainWindow!.editor?.CloseTabByPath(item.Path);

        // x:Bind は OneTime のため、ノードを作り直して差し替える
        IList<TreeViewNode> siblings = item.Parent?.Children ?? ExplorerTree.RootNodes;
        int index = siblings.IndexOf(item);
        if (index < 0) return;
        FileItem newNode = CreateNode(newPath, fileFlag: false);
        siblings[index] = newNode;
        if (isDirectory) AddChildNode(newNode);
    }

    void ClickDeleteItem(object sender, RoutedEventArgs e)
    {
        if (_contextItem is FileItem item) DeleteFileItem(item);
    }

    // SVG をエディタ（テキスト）で開く
    void ClickOpenAsText(object sender, RoutedEventArgs e)
    {
        if (_contextItem is FileItem item) _mainWindow!.editor?.OpenFile(item, FileKind.Text);
    }

    // SVG を WebView で開く
    void ClickOpenAsWebView(object sender, RoutedEventArgs e)
    {
        if (_contextItem is FileItem item) _mainWindow!.editor?.OpenFile(item, FileKind.Svg);
    }

    // ディレクトリ・ファイルを名前順ではなくディレクトリ優先で列挙する
    static string[] ListEntries(string path)
    {
        try
        {
            return [.. Directory.GetDirectories(path, "*"), .. Directory.GetFiles(path, "*")];
        }
        catch
        {
            return [];
        }
    }

    // アイコンと色を割り当てたノードを生成する。fileFlag は子展開の遅延フラグ
    static FileItem CreateNode(string path, bool fileFlag)
    {
        return Directory.Exists(path)
            ? new FileItem(path) { Icon1 = "\xE188", Icon2 = "\xF12B", Color1 = "#FFCF48", Color2 = "#FFE0B2", Flag = true }
            : new FileItem(path) { Icon1 = "\xE132", Icon2 = "\xE130", Color1 = "#9E9E9E", Color2 = "#F5F5F5", Flag = fileFlag };
    }

    static void AddChildNode(FileItem file)
    {
        if (!Directory.Exists(file.Path)) return;
        foreach (string f in ListEntries(file.Path))
        {
            file.Children.Add(CreateNode(f, fileFlag: false));
        }
    }

    void AddTreeViewFiles(string filePath)
    {
        foreach (string f in ListEntries(filePath))
        {
            FileItem file = CreateNode(f, fileFlag: true);
            ExplorerTree.RootNodes.Add(file);
            AddChildNode(file);
        }
    }

    private void ExplorerTreeExpanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        FileItem file = (FileItem)args.Node;
        foreach (FileItem f in file.Children.Cast<FileItem>())
        {
            if (!f.Flag) continue;
            f.Flag = false;
            AddChildNode(f);
        }
    }

    private void ReloadButtonClick(object sender, RoutedEventArgs e)
    {
        _mainWindow!.contentFrame.Navigate(typeof(Explorer), _mainWindow);
        _mainWindow.OpenExplorer.IsEnabled = true;
    }

    private void ClickOpenExplorer(object sender, RoutedEventArgs e)
    {
        _mainWindow!.ClickOpenExplorer(sender, e);
    }

    async void ClickAddNewFile(object sender, RoutedEventArgs e)
    {
        NewFile content = new();
        ContentDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot,
            Title = "ファイル作成",
            PrimaryButtonText = "OK",
            DefaultButton = ContentDialogButton.Primary,
            Content = content
        };
        await dialog.ShowAsync();

        string addFileDir = (ExplorerTree.SelectedItem as FileItem)?.Path ?? _fullFile;
        FileItem addFile = new(addFileDir, content.fileName.Text);
        if (!addFile.CreateFile(_mainWindow!))
            return;

        FileItem fileItem = CreateNode(addFile.Path, fileFlag: false);
        if (ExplorerTree.SelectedItem is FileItem selected)
            selected.Children.Add(fileItem);
        else
            ExplorerTree.RootNodes.Add(fileItem);
    }

    async void ClickAddNewFolder(object sender, RoutedEventArgs e)
    {
        NewFolder content = new();
        ContentDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot,
            Title = "フォルダー作成",
            PrimaryButtonText = "OK",
            DefaultButton = ContentDialogButton.Primary,
            Content = content
        };
        await dialog.ShowAsync();

        string addFileDir = (ExplorerTree.SelectedItem as FileItem)?.Path ?? _fullFile;
        FileItem folder = new(addFileDir, content.folderName.Text);
        if (!folder.CreateDirectory(_mainWindow!))
            return;

        FileItem fileItem = CreateNode(folder.Path, fileFlag: true);
        if (ExplorerTree.SelectedItem is FileItem selected)
            selected.Children.Add(fileItem);
        else
            ExplorerTree.RootNodes.Add(fileItem);
    }

    private void DeleteFileButtonClick(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is FileItem fileItem) DeleteFileItem(fileItem);
    }

    void DeleteFileItem(FileItem fileItem)
    {
        string file = fileItem.Path;
        if (File.Exists(file))
        {
            try
            {
                File.Delete(file);
                RemoveNode(fileItem);
                _mainWindow!.editor?.CloseTabByPath(file);
            }
            catch (IOException err)
            {
                Debug.WriteLine(err.Message);
                Error.Dialog("エラー", err.Message, Content.XamlRoot);
            }
        }
        else if (Directory.Exists(file))
        {
            try
            {
                Directory.Delete(file, recursive: true);
                RemoveNode(fileItem);
            }
            catch (IOException err)
            {
                Error.Dialog("例外: 入出力エラー", err.Message, _mainWindow!.Content.XamlRoot);
            }
        }
    }

    // ルート直下の項目は Parent が無いため RootNodes 側から取り除く
    void RemoveNode(FileItem fileItem)
    {
        if (fileItem.Parent is { } parent)
            parent.Children.Remove(fileItem);
        else
            ExplorerTree.RootNodes.Remove(fileItem);
    }
}
