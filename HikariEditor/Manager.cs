using Microsoft.UI.Xaml.Controls;

namespace HikariEditor
{
    class Manager
    {
        public static MainWindow mainWindow { get; set; }
        public static Frame contentFrame { get; set; }
        public Manager() { }

        public static void openDirectoryMenu()
        {
            //mainWindow.contentFrame
            contentFrame.Navigate(typeof(Explorer), mainWindow);
        }
    }
}
