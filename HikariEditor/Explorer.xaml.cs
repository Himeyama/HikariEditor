// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Diagnostics;
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

            string file = "";
            addChildFiles(file, fullFile, null, ExplorerTree);
            ExplorerTree.ItemInvoked += fileClick;
        }

        void fileClick(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            //Debug.WriteLine(args.InvokedItem.Content);
            string fileName = (string)((TreeViewNode)args.InvokedItem).Content;
            string path = "";
            TreeViewNode parent = ((TreeViewNode)args.InvokedItem).Parent;
            while (parent != null)
            {
                path = string.IsNullOrEmpty(path) ? (string)parent.Content : $"{(string)parent.Content}\\{path}";
                parent = parent.Parent;
            }
            string fileFullPath = $"{fullFile}{path}\\{fileName}";
            Debug.WriteLine(fileFullPath);

            try
            {
                string message = $"open {fileFullPath}";
                string server = "127.0.0.1";
                int port = 8086;
                using TcpClient client = new(server, port);
                byte[] data = Encoding.UTF8.GetBytes(message);
                using NetworkStream stream = client.GetStream();
                stream.Write(data, 0, data.Length);
            }
            catch { }
        }

        void addChildFiles(string file, string fFile, TreeViewNode parent, TreeView root)
        {
            if (!Directory.Exists(fFile)) return;

            // 子ファイルを取得
            string[] fileList = { };
            try
            {
                fileList = Directory.GetDirectories(fFile, "*").Concat(Directory.GetFiles(fFile, "*")).ToArray();
            }
            catch
            {
            }

            // 子ファイル一覧
            List<string> chFiles = new();
            foreach (string f in fileList)
            {
                string[] fs = f.Split("\\");
                chFiles.Add(fs[fs.Count() - 1]);
            }
            for (int i = 0; i < chFiles.Count(); i++)
            {
                string chf = chFiles[i];
                string chfFile = fileList[i];
                TreeViewNode fileNode = new();
                fileNode.Content = chf;
                if (parent != null)
                    parent.Children.Add(fileNode);
                else if (root != null)
                    root.RootNodes.Add(fileNode);
                addChildFiles(chf, chfFile, fileNode, null);
            }
        }
    }
}
