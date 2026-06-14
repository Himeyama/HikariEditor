using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HikariEditor;

public sealed partial class PDF : Page
{
    public PDF()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is PDFPageInfo pdfPageInfo)
            WebView.Source = new Uri(pdfPageInfo.FileItem.Path);

        base.OnNavigatedTo(e);
    }
}
