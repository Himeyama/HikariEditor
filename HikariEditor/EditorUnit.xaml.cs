// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Reflection;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HikariEditor
{
    public sealed partial class EditorUnit : UserControl
    {
        string str2base64(string str)
        {
            byte[] bytesToEncode = System.Text.Encoding.UTF8.GetBytes(str);
            string base64EncodedString = Convert.ToBase64String(bytesToEncode);
            return base64EncodedString;
        }

        public EditorUnit(string fileName)
        {
            InitializeComponent();

            if (File.Exists(fileName))
            {
                string src = "";
                try
                {
                    src = File.ReadAllText(fileName);
                    //Console.WriteLine(text);
                }
                catch (Exception e)
                {
                    Console.WriteLine("ファイルを読み込めませんでした。");
                    Console.WriteLine(e.Message);
                }
                string extension = Path.GetExtension(fileName);
                string b64src = str2base64(src);
                string uri = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\Assets\editor\index.html";
                //Debug.WriteLine(uri);
                //uri += $"?src={b64src}";
                uri += $"?extension={extension}";
                uri += $"&file={str2base64(fileName)}";
                if (ActualTheme == ElementTheme.Light)
                {
                    uri += "&theme=vs-light";
                }
                WebView.Source = new Uri(uri);
                //WebView.NavigationCompleted += setupWebview;
            }
            else
            {

            }
        }

        //async void pasteText(object sender, CoreWebView2NavigationCompletedEventArgs e)
        //{
        //DataPackageView dataPackageView = Clipboard.GetContent();
        //if (dataPackageView.Contains(StandardDataFormats.Text))
        //{
        //    String text = await dataPackageView.GetTextAsync();

        //    // To output the text from this example, you need a TextBlock control
        //    // with a name of "TextOutput".
        //    Debug.WriteLine("Clipboard now contains: " + text);
        //}

        //string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        //string result = await WebView.CoreWebView2.ExecuteScriptAsync($"user_profile = () => {{ return '{userProfile.Replace("\\", "/")}'}}");
        //Debug.WriteLine(result);
        //}
    }
}
