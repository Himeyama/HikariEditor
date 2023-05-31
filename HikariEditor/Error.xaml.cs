// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace HikariEditor
{
    public sealed partial class Error : Page
    {
        public Error()
        {
            this.InitializeComponent();
        }

        async public static void Dialog(string title, string text, XamlRoot XamlRoot)
        {
            ContentDialog dialog = new();
            dialog.XamlRoot = XamlRoot;
            dialog.Title = title;
            dialog.PrimaryButtonText = "‚í‚©‚Á‚½!";
            dialog.DefaultButton = ContentDialogButton.Primary;
            Error content = new();
            dialog.Content = content;
            content.errorText.Text = text;
            await dialog.ShowAsync();
        }
    }
}
