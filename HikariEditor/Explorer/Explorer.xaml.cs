using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Storage;

namespace HikariEditor
{
    public sealed partial class Explorer : Page
    {
        string fullFile;
        ApplicationDataContainer config;
        MainWindow mainWindow;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            mainWindow = e.Parameter as MainWindow;
            base.OnNavigatedTo(e);
        }

        public Explorer()
        {
            config = ApplicationData.Current.LocalSettings;
            InitializeComponent();
            fullFile = config == null ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : (string)config.Values["openDirPath"];
            config.Values["explorerDir"] = fullFile;

            setIcon(@"C:\Windows\System32\imageres.dll", 265, ExplorerIcon);
            setIcon(@"C:\Windows\System32\imageres.dll", 229, ReloadIcon);
            setIcon(@"C:\Windows\System32\imageres.dll", 50, DeleteIcon);

            addTreeViewFiles(fullFile);
            ExplorerTree.ItemInvoked += fileClick;
        }

        // ツリーを選択したとき
        void fileClick(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            FileItem file = args.InvokedItem as FileItem;
            if (file == null) return;
            if (Directory.Exists(file.Path))
            {
                config.Values["explorerDir"] = file.Path;
                return;
            }
            else if (File.Exists(file.Path))
            {
                config.Values["explorerDir"] = Path.GetDirectoryName(file.Path);
            }
            mainWindow.editor.addTab(file.Path, file.Name);
            mainWindow.editorFrame.Height = double.NaN;

            mainWindow.rightArea.ColumnDefinitions[1].Width =
                file.Extension == ".tex" ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        }

        void addChildNode(FileItem file)
        {
            if (!Directory.Exists(file.Path)) return;
            string[] fileList = { };
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

        void addTreeViewFiles(string filePath)
        {
            // 子ファイルを取得
            string[] fileList = { };
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
                addChildNode(file);
            }
        }

        private void ExplorerTree_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
        {
            FileItem file = (FileItem)args.Node;
            foreach (FileItem f in file.Children)
            {
                if (!f.Flag) continue;
                f.Flag = false;
                addChildNode(f);
            }
        }

        private void ReloadButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            mainWindow.contentFrame.Navigate(typeof(Explorer), mainWindow);
            mainWindow.OpenExplorer.IsEnabled = true;
        }

        private void CreateNewFolder(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            FileItem fileItem = ExplorerTree.SelectedItem as FileItem;
            string newDirPath = fileItem == null ? fullFile : fileItem.Path;
            string newFolderName = "New Folder";
            if (CultureInfo.CurrentCulture.Name == "ja-JP")
                newFolderName = "新しいフォルダー";
            newDirPath = $"{newDirPath}\\{newFolderName}";

            if (!Directory.Exists(newDirPath))
            {
                Directory.CreateDirectory(newDirPath);
                return;
            }
            Debug.WriteLine($"CurrentCulture is {CultureInfo.CurrentCulture.Name}.");
            for (int i = 2; i < 1024; i++)
            {
                string nNewDirPath = $"{newDirPath} ({i})";
                if (!Directory.Exists(nNewDirPath))
                {
                    FileItem newFileItem = new(nNewDirPath) { Icon1 = "\xE188", Icon2 = "\xF12B", Color1 = "#FFCF48", Color2 = "#FFE0B2", Flag = true };
                    if (fileItem == null)
                        ExplorerTree.RootNodes.Add(newFileItem);
                    else
                        fileItem.Children.Add(newFileItem);
                    Directory.CreateDirectory(nNewDirPath);
                    return;
                }
            }
        }

        [DllImport("shell32.dll")]
        public static extern int ExtractIconEx(
            string file,
            int index,
            out IntPtr largeIconHandle,
            out IntPtr smallIconHandle,
            int icons
        );

        void setIcon(string iconPath, int iconIndex, Microsoft.UI.Xaml.Controls.BitmapIcon img)
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
            BitmapImage bmpImage = new();
            Uri uri = new(iconResource);
            img.UriSource = uri;
        }

        private void ClickOpenExplorer(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            mainWindow.ClickOpenExplorer(sender, e);
        }

        async void ClickAddNewFile(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new();
            dialog.XamlRoot = this.Content.XamlRoot;
            dialog.Title = "ファイル作成";
            dialog.PrimaryButtonText = "OK";
            dialog.DefaultButton = ContentDialogButton.Primary;
            NewFile content = new();
            dialog.Content = content;
            await dialog.ShowAsync();

            string addFileDir = ((FileItem)ExplorerTree.SelectedItem) == null ? fullFile : ((FileItem)ExplorerTree.SelectedItem).Path;
            string fileName = content.fileName.Text;

            FileItem addFile = new(addFileDir, fileName);
            string addFilePath = addFile.CreateAddFile();

            FileItem fileItem = new(addFilePath) { Icon1 = "\xE132", Icon2 = "\xE130", Color1 = "#9E9E9E", Color2 = "#F5F5F5", Flag = false };
            if (((FileItem)ExplorerTree.SelectedItem) == null)
                ExplorerTree.RootNodes.Add(fileItem);
            else
                ((FileItem)ExplorerTree.SelectedItem).Children.Add(fileItem);
        }

        async void ClickAddNewFolder(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ContentDialog dialog = new();
            dialog.XamlRoot = this.Content.XamlRoot;
            dialog.Title = "フォルダー作成";
            dialog.PrimaryButtonText = "OK";
            dialog.DefaultButton = ContentDialogButton.Primary;
            NewFolder content = new();
            dialog.Content = content;
            await dialog.ShowAsync();

            string addFileDir = ((FileItem)ExplorerTree.SelectedItem) == null ? fullFile : ((FileItem)ExplorerTree.SelectedItem).Path;
            FileItem folder = new(addFileDir, content.folderName.Text);
            string addFolderPath = folder.CreateAddDirectory();

            FileItem fileItem = new(addFolderPath) { Icon1 = "\xE188", Icon2 = "\xF12B", Color1 = "#FFCF48", Color2 = "#FFE0B2", Flag = true };
            if (((FileItem)ExplorerTree.SelectedItem) == null)
                ExplorerTree.RootNodes.Add(fileItem);
            else
                ((FileItem)ExplorerTree.SelectedItem).Children.Add(fileItem);
        }

        private void DeleteFileButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            FileItem fileItem = ExplorerTree.SelectedItem as FileItem;
            string file = fileItem == null ? fullFile : ((FileItem)ExplorerTree.SelectedItem).Path;
            if (File.Exists(file))
            {
                try
                {
                    File.Delete(file);
                    fileItem.Parent.Children.Remove(fileItem);
                }
                catch (IOException err)
                {
                    Debug.WriteLine(err.Message);
                    Error.Dialog("エラー", err.Message, Content.XamlRoot);
                    return;
                }


                //fileItem.cl
            }
            else if (Directory.Exists(file))
            {
                Directory.Delete(file);
            }
            else
            {

            }
        }
    }
}