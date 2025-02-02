using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace HikariEditor
{
    public sealed partial class Explorer : Page
    {
        string fullFile;
        MainWindow? mainWindow;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            mainWindow = (MainWindow)e.Parameter;
            base.OnNavigatedTo(e);
        }

        public Explorer()
        {
            InitializeComponent();

            Settings settings = new();
            settings.LoadSetting();

            if (settings.OpenDirPath == string.Empty)
            {
                fullFile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                settings.ExplorerDir = fullFile;
            }
            else
            {
                fullFile = settings.OpenDirPath;
            }
            settings.SaveSetting();

            SetIcon(@"C:\Windows\System32\imageres.dll", 265, ExplorerIcon);
            SetIcon(@"C:\Windows\System32\imageres.dll", 229, ReloadIcon);
            SetIcon(@"C:\Windows\System32\imageres.dll", 50, DeleteIcon);

            AddTreeViewFiles(fullFile);
            ExplorerTree.ItemInvoked += FileClick;
        }

        // ツリーを選択したとき
        void FileClick(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            FileItem file = (FileItem)args.InvokedItem;
            if (file == null) return;
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
            mainWindow!.editor!.AddTab(file.Path, file.Name);
            mainWindow.editorFrame.Height = double.NaN;

            mainWindow.rightArea.ColumnDefinitions[1].Width =
                file.Extension == ".tex" ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        }

        static void AddChildNode(FileItem file)
        {
            if (!Directory.Exists(file.Path)) return;
            string[] fileList = Array.Empty<string>();
            try
            {
                fileList = Directory.GetDirectories(file.Path, "*").Concat(Directory.GetFiles(file.Path, "*")).ToArray();
            }
            catch { }

            foreach (string f in fileList)
            {
                FileItem chfile = Directory.Exists(f)
                    ? new FileItem(f) { Icon1 = "\xE188", Icon2 = "\xF12B", Color1 = "#FFCF48", Color2 = "#FFE0B2", Flag = true }
                    : new FileItem(f) { Icon1 = "\xE132", Icon2 = "\xE130", Color1 = "#9E9E9E", Color2 = "#F5F5F5", Flag = false };
                file.Children.Add(chfile);
            }
        }

        void AddTreeViewFiles(string filePath)
        {
            // 子ファイルを取得
            string[] fileList = Array.Empty<string>();
            try
            {
                fileList = Directory.GetDirectories(filePath, "*").Concat(Directory.GetFiles(filePath, "*")).ToArray();
            }
            catch
            {
            }

            foreach (string f in fileList)
            {
                FileItem file = Directory.Exists(f)
                    ? new FileItem(f) { Icon1 = "\xE188", Icon2 = "\xF12B", Color1 = "#FFCF48", Color2 = "#FFE0B2", Flag = true }
                    : new FileItem(f) { Icon1 = "\xE132", Icon2 = "\xE130", Color1 = "#9E9E9E", Color2 = "#F5F5F5", Flag = true };
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

        private void ReloadButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            mainWindow!.contentFrame.Navigate(typeof(Explorer), mainWindow);
            mainWindow.OpenExplorer.IsEnabled = true;
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
            Icon icon;
            IntPtr largeIconHandle = IntPtr.Zero;
            IntPtr smallIconHandle = IntPtr.Zero;
            ExtractIconEx(iconPath, iconIndex, out largeIconHandle, out smallIconHandle, 1);
            icon = (Icon)Icon.FromHandle(largeIconHandle).Clone();
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
            //BitmapImage bmpImage = new();
            Uri uri = new(iconResource);
            img.UriSource = uri;
        }

        private void ClickOpenExplorer(object sender, RoutedEventArgs e)
        {
            mainWindow!.ClickOpenExplorer(sender, e);
        }

        async void ClickAddNewFile(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new();
            NewFile content = new();
            dialog.XamlRoot = Content.XamlRoot;
            dialog.Title = "ファイル作成";
            dialog.PrimaryButtonText = "OK";
            dialog.DefaultButton = ContentDialogButton.Primary;
            dialog.Content = content;
            await dialog.ShowAsync();

            string addFileDir = ((FileItem)ExplorerTree.SelectedItem) == null ? fullFile : ((FileItem)ExplorerTree.SelectedItem).Path;
            string fileName = content.fileName.Text;

            FileItem addFile = new(addFileDir, fileName);
            if (!addFile.CreateFile(mainWindow))
            {
                return;
            }

            FileItem fileItem = new(addFile.Path) { Icon1 = "\xE132", Icon2 = "\xE130", Color1 = "#9E9E9E", Color2 = "#F5F5F5", Flag = false };
            if (((FileItem)ExplorerTree.SelectedItem) == null)
                ExplorerTree.RootNodes.Add(fileItem);
            else
                ((FileItem)ExplorerTree.SelectedItem).Children.Add(fileItem);
        }

        async void ClickAddNewFolder(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ContentDialog dialog = new();
            NewFolder content = new();
            dialog.XamlRoot = this.Content.XamlRoot;
            dialog.Title = "フォルダー作成";
            dialog.PrimaryButtonText = "OK";
            dialog.DefaultButton = ContentDialogButton.Primary;
            dialog.Content = content;
            await dialog.ShowAsync();

            string addFileDir = ((FileItem)ExplorerTree.SelectedItem) == null ? fullFile : ((FileItem)ExplorerTree.SelectedItem).Path;
            FileItem folder = new(addFileDir, content.folderName.Text);
            if (!folder.CreateDirectory(mainWindow))
            {
                return;
            }

            FileItem fileItem = new(folder.Path) { Icon1 = "\xE188", Icon2 = "\xF12B", Color1 = "#FFCF48", Color2 = "#FFE0B2", Flag = true };
            if (((FileItem)ExplorerTree.SelectedItem) == null)
                ExplorerTree.RootNodes.Add(fileItem);
            else
                ((FileItem)ExplorerTree.SelectedItem).Children.Add(fileItem);
        }

        private void DeleteFileButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            FileItem? fileItem = ExplorerTree.SelectedItem as FileItem;
            string file = fileItem == null ? fullFile : ((FileItem)ExplorerTree.SelectedItem).Path;
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
                    return;
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
                    Error.Dialog("例外: 入出力エラー", err.Message, mainWindow!.Content.XamlRoot);
                }
            }
            else
            {

            }
        }
    }
}