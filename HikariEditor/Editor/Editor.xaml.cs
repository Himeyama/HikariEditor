using Microsoft.UI.Xaml;
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
using Windows.ApplicationModel.DataTransfer;

namespace HikariEditor
{
    public sealed partial class Editor : Page
    {
        List<string> tabs = new() { };
        MainWindow mainWindow;
        int counter = 0;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            mainWindow = e.Parameter as MainWindow;
            mainWindow.editor = this;
            base.OnNavigatedTo(e);
        }

        public Editor()
        {
            InitializeComponent();
            waitServer();
        }

        async public void CallPasteFunction(string text)
        {
            // 貼付機能
            TabViewItem tab = (TabViewItem)Tabs.SelectedItem;
            if (tab == null) return;
            EditorUnit editorUnit = tab.Content as EditorUnit;
            if (editorUnit == null) return;
            WebView2 webView = editorUnit.WebView;
            if (webView == null) return;
            string encText = new Text(text).EncodeBase64();
            await webView.ExecuteScriptAsync($"paste_text('{encText}')");
        }

        async public void CallCopyFunction()
        {
            // コピー機能
            TabViewItem tab = (TabViewItem)Tabs.SelectedItem;
            if (tab == null) return;
            EditorUnit editorUnit = tab.Content as EditorUnit;
            if (editorUnit == null) return;
            WebView2 webView = editorUnit.WebView;
            if (webView == null) return;
            await webView.ExecuteScriptAsync($"copy_text()");
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

        private void TabViewAddTabButtonClick(TabView sender, object args)
        {
            TabViewItem newTab = new();
            newTab.IconSource = new SymbolIconSource() { Symbol = Symbol.Document };
            newTab.Header = "Untitled";
            Frame frame = new();
            newTab.Content = frame;
            newTab.IsSelected = true;
            sender.TabItems.Add(newTab);
        }

        private void TabViewCloseTab(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            tabs.Remove(args.Tab.Name);
            sender.TabItems.Remove(args.Tab);
            if (tabs.Count == 0)
            {
                mainWindow.editorFrame.Height = 0;
                mainWindow.previewFrame.Height = 0;
            }
        }

        async void waitServer()
        {
            while (true)
            {
                await server();
            }
        }

        public void addTab(string fileName, string shortFileName)
        {
            // タブが存在する場合
            if (tabs.Contains(fileName))
            {
                TabViewItem tab = (TabViewItem)Tabs.FindName(fileName);
                if (tab != null)
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

                if (commands[0..4] == "POST")
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
                            FileItem fileItem = new(fileName);
                            string srcCode64 = string.Join(Environment.NewLine, srcs[1..^0]);
                            string srcCode = b642str(srcCode64);
                            Debug.WriteLine($"=== {fileItem.Name} ===\n{srcCode}\n===");
                            fileItem.Save(srcCode, mainWindow.NLBtn.Content.ToString());
                            mainWindow.StatusBar.Text = $"{fileItem.Name} を保存しました";
                            counter++;
                            DelayResetStatusBar(1000);
                            if (fileItem.Extension == ".tex")
                            {
                                mainWindow.StatusBar.Text = $"{fileItem.Name} を保存しました。TeX のコンパイルを行います。";
                                counter++;
                                DelayResetStatusBar(1000);

                                bool tex_compile_error = false;
                                try
                                {
                                    using (Process process = new())
                                    {
                                        process.StartInfo.UseShellExecute = false;
                                        process.StartInfo.FileName = "C:\\texlive\\2022\\bin\\win32\\ptex2pdf.exe";
                                        process.StartInfo.CreateNoWindow = true;
                                        process.StartInfo.Arguments = $"-l -ot -interaction=nonstopmode -halt-on-error -kanji=utf8 -output-directory=\"{fileItem.Dirname}\" \"{fileItem.Path}\"";
                                        process.StartInfo.RedirectStandardOutput = true;
                                        process.Start();
                                        process.WaitForExit();
                                        string stdout = process.StandardOutput.ReadToEnd();
                                        Debug.WriteLine(stdout);
                                        if (process.ExitCode == 0)
                                        {
                                            mainWindow.StatusBar.Text = $"{fileItem.Name} のコンパイルに成功しました。";
                                        }
                                        else
                                        {
                                            mainWindow.StatusBar.Text = $"{fileItem.Name} のコンパイルに失敗しました。";
                                            tex_compile_error = true;
                                        }
                                        counter++;
                                        DelayResetStatusBar(1000);
                                    }

                                    if (!tex_compile_error)
                                    {
                                        FileItem pdfFileItem = new(fileItem.Dirname, $"{fileItem.WithoutName}.pdf");
                                        PDFPageInfo pdfPageInfo = new();
                                        pdfPageInfo.mainWindow = mainWindow;
                                        pdfPageInfo.fileItem = pdfFileItem;
                                        Debug.WriteLine(pdfFileItem.Path);
                                        mainWindow.previewFrame.Navigate(typeof(PDF), pdfPageInfo);
                                    }

                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e.Message);
                                }
                            }
                        }

                        if (httpCommand.Length >= 8 && httpCommand[0..8] == "autosave")
                        {
                            string src = httpCommand[0..8];
                            string[] srcs = httpCommand[9..^0].Split('\n');
                            string fileName = b642str(srcs[0]);
                            FileItem fileItem = new(fileName);
                            string srcCode = string.Join(Environment.NewLine, srcs[1..^0]);
                            if (!mainWindow.AutoSave.IsChecked)
                                return;
                            Debug.WriteLine($"=== 自動保存: {fileItem.Name} ===\n{srcCode}\n===");
                            fileItem.Save(srcCode, mainWindow.NLBtn.Content.ToString());
                            mainWindow.StatusBar.Text = $"{fileItem.Name} を自動保存しました";
                            counter++;
                            DelayResetStatusBar(1000);
                        }

                        if (httpCommand.Length >= 14 && httpCommand[0..14] == "copy-clipboard")
                        {
                            string src = httpCommand[0..14];
                            string[] srcs = httpCommand[15..^0].Split('\n');
                            string fileName = b642str(srcs[0]);
                            FileItem fileItem = new(fileName);
                            string srcCode = string.Join(Environment.NewLine, srcs[1..^0]);
                            Debug.WriteLine($"=== コピー: {fileItem.Name} ===\n{srcCode}\n===");
                            DataPackage dataPackage = new();
                            dataPackage.SetText(srcCode);
                            Clipboard.SetContent(dataPackage);
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
            }
            finally
            {
                listener.Stop();
            }
        }

        async void DelayResetStatusBar(int sec)
        {
            int count = counter;
            await Task.Delay(TimeSpan.FromMilliseconds(sec));
            if (count >= counter)
            {
                mainWindow.StatusBar.Text = "";
            }
        }

        private void EditorTabChange(object sender, SelectionChangedEventArgs e)
        {
            FrameworkElement selectedItem = (FrameworkElement)((TabView)sender).SelectedItem;
            if (selectedItem == null) return;
            string fileName = ((FrameworkElement)((TabView)sender).SelectedItem).Name;
            string extension = Path.GetExtension(fileName);
            if (extension == ".tex")
            {
                mainWindow.rightArea.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                mainWindow.rightArea.ColumnDefinitions[1].Width = new GridLength(0);
            }
        }
    }
}
