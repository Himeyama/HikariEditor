using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HikariEditor;

public sealed partial class Error : Page
{
    public Error()
    {
        InitializeComponent();
    }

    public static async void Dialog(string title, string text, XamlRoot xamlRoot)
    {
        Error content = new();
        content.errorText.Text = text;
        ContentDialog dialog = new()
        {
            XamlRoot = xamlRoot,
            Title = title,
            PrimaryButtonText = "わかりました",
            DefaultButton = ContentDialogButton.Primary,
            Content = content
        };
        await dialog.ShowAsync();
    }
}
