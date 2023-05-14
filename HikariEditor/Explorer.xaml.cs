// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HikariEditor
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Explorer : Page
    {
        string fullFile;
        ApplicationDataContainer config;


        public Explorer()
        {
            config = ApplicationData.Current.LocalSettings;
            InitializeComponent();
            fullFile = config == null ? "C:\\Users\\minan" : (string)config.Values["openDirPath"];

            //string file = "";
            //addChildFiles(file, fullFile, null, ExplorerTree);
            addTreeViewFiles(fullFile);
            ExplorerTree.ItemInvoked += fileClick;
        }

        void fileClick(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            FileItem file = args.InvokedItem as FileItem;
            if (file == null) return;

            try
            {
                string message = $"open {file.Path}";
                string server = "127.0.0.1";
                int port = 8086;
                using TcpClient client = new(server, port);
                byte[] data = Encoding.UTF8.GetBytes(message);
                using NetworkStream stream = client.GetStream();
                stream.Write(data, 0, data.Length);
            }
            catch { }
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
                    ? new FileItem { Path = f, Name = Path.GetFileName(f), Icon1 = "\xE188", Icon2 = "\xF12B", Color1 = "#FFCF48", Color2 = "#FFE0B2", Flag = true }
                    : new FileItem { Path = f, Name = Path.GetFileName(f), Icon1 = "\xE132", Icon2 = "\xE130", Color1 = "#9E9E9E", Color2 = "#F5F5F5", Flag = true };
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
                    ? new FileItem { Path = f, Name = Path.GetFileName(f), Icon1 = "\xE188", Icon2 = "\xF12B", Color1 = "#FFCF48", Color2 = "#FFE0B2", Flag = true }
                    : new FileItem { Path = f, Name = Path.GetFileName(f), Icon1 = "\xE132", Icon2 = "\xE130", Color1 = "#9E9E9E", Color2 = "#F5F5F5", Flag = true };
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
    }
}
