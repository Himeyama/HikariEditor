using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HikariEditor
{
    public sealed partial class PDF : Page
    {
        MainWindow? mainWindow;
        FileItem? fileItem;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            PDFPageInfo? pdfPageInfo = e.Parameter as PDFPageInfo;
            mainWindow = pdfPageInfo!.mainWindow;
            fileItem = pdfPageInfo!.fileItem;

            WebView.Source = new Uri(fileItem!.Path);

            base.OnNavigatedTo(e);
        }

        public PDF()
        {
            InitializeComponent();
        }


    }
}
