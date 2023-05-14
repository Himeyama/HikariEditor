using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HikariEditor
{
    public sealed partial class Editor : Page
    {
        List<string> tabs = new() { };
        MainWindow mainWindow;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            mainWindow = e.Parameter as MainWindow;
            base.OnNavigatedTo(e);
        }

        public Editor()
        {
            InitializeComponent();
            waitServer();
        }

        string str2b64(string str)
        {
            byte[] bytesToEncode = System.Text.Encoding.UTF8.GetBytes(str);
            string base64EncodedString = Convert.ToBase64String(bytesToEncode);
            return base64EncodedString;
        }

        string b642str(string b64str)
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

        private void TabView_AddTabButtonClick(TabView sender, object args)
        {
            TabViewItem newTab = new();
            newTab.IconSource = new SymbolIconSource() { Symbol = Symbol.Document };
            newTab.Header = "Untitled";
            Frame frame = new();
            newTab.Content = frame;
            newTab.IsSelected = true;
            sender.TabItems.Add(newTab);
        }

        private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            tabs.Remove(args.Tab.Name);
            sender.TabItems.Remove(args.Tab);
        }

        async void waitServer()
        {
            while (true)
            {
                await server();
            }
        }

        void addTab(string fileName, string shortFileName)
        {
            // タブが存在する場合
            if (tabs.Contains(fileName))
            {
                TabViewItem tab = (TabViewItem)Tabs.FindName(fileName);
                tab.IsSelected = true;
                return;
            }
            tabListAdd(fileName);
            if (!File.Exists(fileName)) return;
            TabViewItem newTab = new();
            newTab.IconSource = new SymbolIconSource() { Symbol = Symbol.Document };
            newTab.Header = shortFileName;
            EditorUnit frame = new(fileName);
            newTab.Content = frame;
            newTab.Name = fileName;
            newTab.IsSelected = true;
            Tabs.TabItems.Add(newTab);
        }

        void tabListAdd(string fileName)
        {
            if (tabs.Contains(fileName)) return;
            tabs.Add(fileName);
        }

        string str2MD5(string src)
        {
            byte[] srcBytes = Encoding.UTF8.GetBytes(src);
            string MD5src;
            using (MD5 md5 = MD5.Create())
            {
                byte[] MD5srcBytes = md5.ComputeHash(srcBytes);
                StringBuilder sb = new();
                for (int i = 0; i < MD5srcBytes.Length; i++)
                    sb.Append(MD5srcBytes[i].ToString("x2"));
                MD5src = sb.ToString();
            }
            return MD5src;
        }

        async Task server()
        {
            IPAddress ipaddr = IPAddress.Parse("127.0.0.1");
            IPEndPoint ipEndPoint = new(ipaddr, 8086);
            TcpListener listener = new(ipEndPoint);

            try
            {
                listener.Start();
                using TcpClient handler = await listener.AcceptTcpClientAsync();

                byte[] buffer = new byte[1024];
                using NetworkStream stream = handler.GetStream();
                stream.Read(buffer, 0, buffer.Length);
                string commands = Encoding.UTF8.GetString(buffer, 0, buffer.Length);
                // HTTP リクエストの場合

                if (commands[0..3] == "GET")
                {
                    string[] data = commands.Split(' ');

                    // 保存処理
                    string parameter = data[1];
                    string pattern = @"^/\?data=(.*)$";
                    Match match = Regex.Match(parameter, pattern);
                    if (match.Success)
                    {
                        string b64Command = match.Groups[1].Value;
                        string httpCommand = b642str(b64Command);
                        //byte[] b64bytes = Convert.FromBase64String(b64Command);
                        //string httpCommand = Encoding.UTF8.GetString(b64bytes);

                        /*
                        data=(base64)
                        ===
                        save FileName(base64)
                        src...
                        ===
                        ex: http://127.0.0.1:8086/?data=c2F2ZSBRenBjCnB1dHMgIkhlbGxvISI=
                        */
                        if (httpCommand.Length >= 4 && httpCommand[0..4] == "save")
                        {
                            string src = httpCommand[0..4];
                            string[] srcs = httpCommand[5..^0].Split('\n');
                            string fileName = b642str(srcs[0]);
                            string shortFileName = Path.GetFileName(fileName);
                            string srcCode = string.Join(Environment.NewLine, srcs[1..^0]);

                            Debug.WriteLine($"=== {shortFileName} ===\n{srcCode}\n===");
                            File.WriteAllText(fileName, srcCode);
                            mainWindow.StatusBar.Text = $"{shortFileName} を保存しました";
                            DelayResetStatusBar(3);
                        }

                        if (httpCommand.Length >= 8 && httpCommand[0..8] == "autosave")
                        {
                            string src = httpCommand[0..8];
                            string[] srcs = httpCommand[9..^0].Split('\n');
                            string fileName = b642str(srcs[0]);
                            string shortFileName = Path.GetFileName(fileName);
                            string srcCode = string.Join(Environment.NewLine, srcs[1..^0]);
                            Debug.WriteLine($"=== 自動保存: {shortFileName} ===\n{srcCode}\n===");

                            if (!mainWindow.AutoSave.IsChecked)
                                return;

                            File.WriteAllText(fileName, srcCode);
                            mainWindow.StatusBar.Text = $"{shortFileName} を自動保存しました";
                            DelayResetStatusBar(3);
                        }
                    }

                    // ファイルを開く処理
                    pattern = @"^/\?open=(.*)$";
                    match = Regex.Match(parameter, pattern);
                    if (match.Success)
                    {
                        string b64file = match.Groups[1].Value;
                        string fileName = b642str(b64file);
                        string src = "";
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
                        /*
                        open=filename(base64)
                        ex: http://127.0.0.1:8086?open=QzpcVXNlcnNcbWluYW5ccm9vdFxEb2N1bWVudHNcdW50aXRsZWQxLnJi
                        */
                        string log = $"=== 読み込み: {fileName} ===\n";
                        log += src;
                        log += "\n======";
                        Debug.WriteLine(log);
                        string b64src = str2b64(src);

                        // MD5 を取得
                        string md5src = str2MD5(src);

                        byte[] responseBytes = Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {b64src.Length}\r\nAccess-Control-Allow-Origin: *\r\n\r\n{b64src}");
                        stream.Write(responseBytes, 0, responseBytes.Length);
                        byte[] bufferB64src = Encoding.UTF8.GetBytes(b64src);
                        stream.Write(bufferB64src, 0, bufferB64src.Length);
                    }
                }

                //スペース区切り
                string[] sCommands = commands.Split(' ');
                string command = sCommands[0];
                if (command == "open")
                {
                    string fileName = string.Join(" ", sCommands[1..^0]).TrimEnd('\0');
                    string shortFileName = Path.GetFileName(fileName).TrimEnd('\0');
                    addTab(fileName, shortFileName);
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        private async void DelayResetStatusBar(int sec)
        {
            // 連続して保存すると表示が短くなるバグあり
            await Task.Delay(TimeSpan.FromSeconds(sec));
            mainWindow.StatusBar.Text = "";
        }
    }
}
