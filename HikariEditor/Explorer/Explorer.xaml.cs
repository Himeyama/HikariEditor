using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace HikariEditor;

public sealed partial class Explorer : Page
{
    string _fullFile;
    MainWindow? _mainWindow;

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

        SetIcon(@"C:\Windows\System32\imageres.dll", 265, ExplorerIcon);
        SetIcon(@"C:\Windows\System32\imageres.dll", 229, ReloadIcon);
        SetIcon(@"C:\Windows\System32\imageres.dll", 50, DeleteIcon);

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
        _mainWindow!.editor!.AddTab(file.Path, file.Name);
        _mainWindow.editorFrame.Height = double.NaN;

        _mainWindow.rightArea.ColumnDefinitions[1].Width =
            file.Extension == ".tex" ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
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

    [DllImport("shell32.dll")]
    public static extern int ExtractIconEx(
        string file,
        int index,
        out IntPtr largeIconHandle,
        out IntPtr smallIconHandle,
        int icons
    );

    void SetIcon(string iconPath, int iconIndex, BitmapIcon img)
    {
        ExtractIconEx(iconPath, iconIndex, out IntPtr largeIconHandle, out _, 1);
        Icon icon = (Icon)Icon.FromHandle(largeIconHandle).Clone();
        string tmpDir = $"{Path.GetTempPath()}HikariEditor\\";
        if (!Directory.Exists(tmpDir))
            Directory.CreateDirectory(tmpDir);
        string iconFileName = Path.GetFileNameWithoutExtension(iconPath);
        string iconResource = $"{tmpDir}{iconFileName}-{iconIndex}.png";
        if (!File.Exists(iconResource))
        {
            using Bitmap bmp = icon.ToBitmap();
            bmp.Save(iconResource);
        }
        img.UriSource = new Uri(iconResource);
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
        FileItem? fileItem = ExplorerTree.SelectedItem as FileItem;
        string file = fileItem?.Path ?? _fullFile;
        if (File.Exists(file))
        {
            try
            {
                File.Delete(file);
                fileItem!.Parent.Children.Remove(fileItem);
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
                Directory.Delete(file);
            }
            catch (IOException err)
            {
                Error.Dialog("例外: 入出力エラー", err.Message, _mainWindow!.Content.XamlRoot);
            }
        }
    }
}
