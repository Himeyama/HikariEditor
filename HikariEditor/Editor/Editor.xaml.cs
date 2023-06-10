using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Windows.ApplicationModel.DataTransfer;

namespace HikariEditor
{
    public sealed partial class Editor : Page
    {
        readonly List<string> tabs = new() { };
        MainWindow? mainWindow;
        private int counter = 0;

        public List<string> Tabs1 => tabs;

        public MainWindow? MainWindow { get => mainWindow; set => mainWindow = value; }
        public int Counter { get => counter; set => counter = value; }

        struct PostInfo
        {
            private string top;
            private string body;

            public string Top { readonly get => top; set => top = value; }
            public string Body { readonly get => body; set => body = value; }
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            MainWindow = e.Parameter as MainWindow;
            MainWindow!.editor = this;
            base.OnNavigatedTo(e);
        }

        public Editor()
        {
            InitializeComponent();
            WaitServer();
        }

        async public void CallPasteFunction(string text)
        {
            // 貼付機能
            TabViewItem tab = (TabViewItem)Tabs.SelectedItem;
            if (tab == null) return;
            EditorUnit? editorUnit = tab.Content as EditorUnit;
            //if (editorUnit == null) return;
            WebView2 webView = editorUnit!.WebView;
            //if (webView == null) return;
            string encText = new Text(text).EncodeBase64();
            await webView.ExecuteScriptAsync($"paste_text('{encText}')");
        }

        async public void CallCopyFunction()
        {
            // コピー機能
            TabViewItem tab = (TabViewItem)Tabs.SelectedItem;
            EditorUnit? editorUnit = tab.Content as EditorUnit;
            WebView2 webView = editorUnit!.WebView;
            if (webView == null) return;
            await webView.ExecuteScriptAsync($"copy_text()");
        }

        static string Str2Base64(string str)
        {
            byte[] bytesToEncode = System.Text.Encoding.UTF8.GetBytes(str);
            string base64EncodedString = Convert.ToBase64String(bytesToEncode);
            return base64EncodedString;
        }

        static string Base642Str(string b64str)
        {
            byte[] b64bytes;
            int mod4 = b64str.Length % 4;
            if (mod4 > 0)
                b64str += new string('=', 4 - mod4);

            try
            {
                b64bytes = Convert.FromBase64String(b64str);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Base64 エンコードができません");
                Debug.WriteLine(e);
                return "";
            }

            return Encoding.UTF8.GetString(b64bytes);
        }

        //static void TabViewAddTabButtonClick(TabView sender, object args)
        //{
        //    TabViewItem newTab = new();
        //    newTab.IconSource = new SymbolIconSource() { Symbol = Symbol.Document };
        //    newTab.Header = "Untitled";
        //    Frame frame = new();
        //    newTab.Content = frame;
        //    newTab.IsSelected = true;
        //    sender.TabItems.Add(newTab);
        //}

        private void TabViewCloseTab(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            Tabs1.Remove(args.Tab.Name);
            sender.TabItems.Remove(args.Tab);
            if (Tabs1.Count == 0)
            {
                MainWindow!.editorFrame.Height = 0;
                MainWindow.previewFrame.Height = 0;
            }
        }

        async void WaitServer()
        {
            while (true)
            {
                await Server();
            }
        }

        public void AddTab(string fileName, string shortFileName)
        {
            // タブが存在する場合
            if (Tabs1.Contains(fileName))
            {
                TabViewItem tab = (TabViewItem)Tabs.FindName(fileName);
                if (tab != null)
                    tab.IsSelected = true;
                return;
            }
            TabListAdd(fileName);
            if (!File.Exists(fileName)) return;
            EditorUnit frame = new(fileName);
            TabViewItem newTab = new()
            {
                IconSource = new SymbolIconSource() { Symbol = Symbol.Document },
                Header = shortFileName,
                Content = frame,
                Name = fileName,
                IsSelected = true
            };
            Tabs.TabItems.Add(newTab);
        }

        void TabListAdd(string fileName)
        {
            if (Tabs1.Contains(fileName)) return;
            Tabs1.Add(fileName);
        }

        //static string Str2MD5(string src)
        //{
        //    byte[] srcBytes = Encoding.UTF8.GetBytes(src);
        //    string MD5src;
        //    byte[] MD5srcBytes = MD5.HashData(srcBytes);
        //    StringBuilder sb = new();
        //    for (int i = 0; i < MD5srcBytes.Length; i++)
        //        sb.Append(MD5srcBytes[i].ToString("x2"));
        //    MD5src = sb.ToString();
        //    return MD5src;
        //}

        async Task Server()
        {
            IPAddress ipaddr = IPAddress.Parse("127.0.0.1");
            IPEndPoint ipEndPoint = new(ipaddr, 8086);
            TcpListener listener = new(ipEndPoint);

            try
            {
                listener.Start();
                using TcpClient handler = await listener.AcceptTcpClientAsync();
                using NetworkStream stream = handler.GetStream();
                stream.Socket.ReceiveBufferSize = 67108864;
                byte[] buffer = new byte[stream.Socket.ReceiveBufferSize];
                stream.Read(buffer, 0, buffer.Length);

                string commands = Encoding.UTF8.GetString(buffer, 0, buffer.Length);

                // HTTP リクエストの場合
                if (commands[0..4] == "POST")
                {
                    //PostInfo postInfo = new PostInfo();
                    PostInfo postInfo = ReadPost(commands);
                    string top = postInfo.Top;
                    string body = postInfo.Body;

                    Debug.WriteLine(body);
                    string[] data = top.Split(' ');

                    /* 保存処理 パラメータの取得 */
                    string parameter = data[1];
                    string pattern = @"^/\?data=(.*)$";
                    Match match = Regex.Match(parameter, pattern);
                    if (match.Success)
                    {
                        string b64Command = match.Groups[1].Value;
                        /* パラメータのデコード */
                        string httpCommand = Base642Str(b64Command);

                        FileSave(body, httpCommand);
                        AutoSave(body, httpCommand);
                        CopyClipboard(httpCommand);
                    }

                    // ファイルを開く処理
                    FileOpen(parameter, stream);
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        static PostInfo ReadPost(string commands)
        {
            PostInfo postInfo = new();
            string? top;
            string? body;
            using (StringReader? stringReader = new(commands))
            {
                top = stringReader.ReadLine();
                int nLine = 0;
                string? line = stringReader.ReadLine();
                while (line != string.Empty)
                {
                    line = stringReader.ReadLine();
                    nLine++;
                    if (nLine == 64) break;
                }
                body = stringReader.ReadLine()!.TrimEnd('\0');
            }
            postInfo.Top = top!;
            postInfo.Body = body;
            return postInfo;
        }

        static void FileOpen(string parameter, NetworkStream stream)
        {
            string pattern = @"^/\?open=(.*)$";
            Match match = Regex.Match(parameter, pattern);
            if (match.Success)
            {
                string b64file = match.Groups[1].Value;
                string fileName = Base642Str(b64file);
                string src;
                try
                {
                    src = File.ReadAllText(fileName);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(fileName);
                    Debug.WriteLine("ファイルを読み込めませんでした。");
                    Debug.WriteLine(e.Message);
                    return;
                }
                string log = $"=== 読み込み: {fileName} ===\n";
                log += src;
                log += "\n======";
                Debug.WriteLine(log);
                string b64src = Str2Base64(src);

                byte[] responseBytes = Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {b64src.Length}\r\nAccess-Control-Allow-Origin: *\r\n\r\n{b64src}");
                stream.Write(responseBytes, 0, responseBytes.Length);
                byte[] bufferB64src = Encoding.UTF8.GetBytes(b64src);
                stream.Write(bufferB64src, 0, bufferB64src.Length);
            }
        }

        void FileSave(string body, string httpCommand)
        {
            if (httpCommand.Length >= 4 && httpCommand[0..4] == "save")
            {
                string[] srcs = httpCommand[5..^0].Split('\n');
                string fileName = Base642Str(srcs[0]); /* ファイル名 */
                FileItem fileItem = new(fileName);
                string srcCode = Base642Str(body);
                Debug.WriteLine($"=== {fileItem.Name} ===\n{srcCode}\n===");
                fileItem.Save(srcCode, MainWindow!.NLBtn.Content.ToString());
                MainWindow.StatusBar.Text = $"{fileItem.Name} を保存しました。";
                LogPage.AddLog(MainWindow, $"{fileItem.Name} を保存しました。");
                Counter++;
                DelayResetStatusBar(1000);
                if (fileItem.Extension == ".tex")
                {
                    MainWindow.StatusBar.Text = $"{fileItem.Name} を保存しました。TeX のコンパイルを実行しています...";
                    LogPage.AddLog(MainWindow, "LaTeX のコンパイルを実行しています...");
                    Counter++;
                    DelayResetStatusBar(1000);
                    _ = LaTeX.Compile(MainWindow, fileItem, this);
                }
            }
        }

        void AutoSave(string body, string httpCommand)
        {
            if (httpCommand.Length >= 8 && httpCommand[0..8] == "autosave")
            {
                //string src = httpCommand[0..8];
                string[] srcs = httpCommand[9..^0].Split('\n');
                string fileName = Base642Str(srcs[0]);
                FileItem fileItem = new(fileName);
                string srcCode = Base642Str(body);
                if (!MainWindow!.AutoSave.IsChecked)
                    return;
                Debug.WriteLine($"=== 自動保存: {fileItem.Name} ===\n{srcCode}\n===");
                fileItem.Save(srcCode, MainWindow.NLBtn.Content.ToString());
                MainWindow.StatusBar.Text = $"{fileItem.Name} を自動保存しました。";
                LogPage.AddLog(MainWindow, $"{fileItem.Name} を自動保存しました。");
                Counter++;
                DelayResetStatusBar(1000);
            }
        }

        static void CopyClipboard(string httpCommand)
        {
            if (httpCommand.Length >= 14 && httpCommand[0..14] == "copy-clipboard")
            {
                //string src = httpCommand[0..14];
                string[] srcs = httpCommand[15..^0].Split('\n');
                string fileName = Base642Str(srcs[0]);
                FileItem fileItem = new(fileName);
                string srcCode = string.Join(Environment.NewLine, srcs[1..^0]);
                Debug.WriteLine($"=== コピー: {fileItem.Name} ===\n{srcCode}\n===");
                DataPackage dataPackage = new();
                dataPackage.SetText(srcCode);
                Clipboard.SetContent(dataPackage);
            }
        }

        public async void DelayResetStatusBar(int sec)
        {
            int count = Counter;
            await Task.Delay(TimeSpan.FromMilliseconds(sec));
            if (count >= Counter)
            {
                MainWindow!.StatusBar.Text = "";
            }
        }

        private void EditorTabChange(object sender, SelectionChangedEventArgs e)
        {
            FrameworkElement selectedItem = (FrameworkElement)((TabView)sender).SelectedItem;
            if (selectedItem == null) return;
            string fileName = ((FrameworkElement)((TabView)sender).SelectedItem).Name;
            string extension = System.IO.Path.GetExtension(fileName);
            if (extension == ".tex")
            {
                MainWindow!.rightArea.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                MainWindow!.rightArea.ColumnDefinitions[1].Width = new GridLength(0);
            }
        }
    }
}
