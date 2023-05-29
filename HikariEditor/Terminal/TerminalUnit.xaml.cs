// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Storage;

namespace HikariEditor
{
    public sealed partial class TerminalUnit : Page
    {
        public TerminalUnit()
        {
            InitializeComponent();

            ViewWebView();
        }


        async void ViewWebView()
        {
            string theme = "light";
            if (ActualTheme == ElementTheme.Dark)
            {
                theme = "dark";
            }
            StorageFile htmlFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Terminal/index.html?theme={theme}"));
            WebViewTerminal.Source = new Uri(htmlFile.Path + $"?theme={theme}");
        }
    }
}