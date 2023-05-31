using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;

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

            /* ƒ^ƒu‚ðŠJ‚­ */
            if (File.Exists(fileName))
            {
                string extension = Path.GetExtension(fileName);
                string tempDirectory = Path.GetTempPath();
                string editorDir = $"{tempDirectory}HikariEditor";
                string uri = $"{editorDir}\\editor\\index.html";
                uri += $"?extension={extension}";
                uri += $"&file={str2base64(fileName)}";
                if (ActualTheme == ElementTheme.Light)
                {
                    uri += "&theme=vs-light";
                }
                WebView.Source = new Uri(uri);
            }
        }
    }
}
