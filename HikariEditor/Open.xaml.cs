using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;


namespace HikariEditor
{
    public sealed partial class Open : Page
    {
        List<Directories>? items;
        Frame? explorerFrame;
        MainWindow? mainWindow;
        string? currentDir;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e != null && e.Parameter != null)
                mainWindow = (MainWindow)e.Parameter;

            if (mainWindow != null)
                explorerFrame = mainWindow.contentFrame;

            base.OnNavigatedTo(e);
        }

        public Open()
        {
            InitializeComponent();
            DirOpenHome();
        }

        private void Directories_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if ((Directories)Directories.SelectedValue == null)
                return;
            DirOpenParentBtn.IsEnabled = true;
            string? dir = ((Directories)Directories.SelectedValue).Path;
            currentDir = dir;
            DirPath.Text = dir;
            string[] dirs = Directory.GetDirectories(dir!);
            items = new();
            foreach (string d in dirs)
            {
                items.Add(new Directories { Path = d, Name = Path.GetFileName(d) });
            }
            Directories.ItemsSource = items;
            OpenBtn.IsEnabled = true;
        }

        private void DirOpenHomeBtnClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            DirOpenHome();
        }

        void DirOpenHome()
        {
            DirOpenParentBtn.IsEnabled = true;
            items = new();
            string? homeDir = Environment.GetEnvironmentVariable("userprofile");
            currentDir = homeDir;
            if (homeDir == null)
            {
                Error.Dialog("���ϐ�����`�G���[", "���ϐ�������`�ł��B", mainWindow!.Content.XamlRoot);
                return;
            };
            string[] homeDirs = Directory.GetDirectories(homeDir);
            foreach (string dir in homeDirs)
            {
                items.Add(new Directories { Path = dir, Name = Path.GetFileName(dir) });
            }
            DirPath.Text = homeDir;
            Directories.ItemsSource = items;
            OpenBtn.IsEnabled = true;
        }

        void DirOpenComputer()
        {
            DirOpenParentBtn.IsEnabled = false;
            items = new();
            foreach (string drive in Directory.GetLogicalDrives())
            {
                items.Add(new Directories { Path = drive, Name = drive });
            }
            DirPath.Text = "";
            currentDir = "";
            Directories.ItemsSource = items;
            OpenBtn.IsEnabled = true;
        }

        void DirOpenComputerClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            DirOpenComputer();
        }

        void DirOpenParentClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            items = new();
            string dir = DirPath.Text;
            if (string.IsNullOrEmpty(dir))
                return;
            if (currentDir == null)
            {
                Error.Dialog("�ϐ�����`�G���[", "���݂̃f�B���N�g��������`�ł��B", mainWindow!.Content.XamlRoot);
                return;
            }
            DirectoryInfo? parentDirInfo = Directory.GetParent(currentDir);
            if (parentDirInfo == null)
            {
                DirOpenComputer();
                return;
            }
            string parentDir = parentDirInfo.FullName;
            string[] parentDirs = Directory.GetDirectories(parentDir);
            foreach (string d in parentDirs)
            {
                items.Add(new Directories { Path = d, Name = Path.GetFileName(d) });
            }
            DirPath.Text = parentDir;
            currentDir = parentDir;
            Directories.ItemsSource = items;
            if (currentDir == "")
                OpenBtn.IsEnabled = false;
        }

        // �J���{�^���̃N���b�N
        private void OpenBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Settings settings = new();
            string openDirPath = DirPath.Text;
            settings.OpenDirPath = openDirPath;
            settings.SaveSetting();
            explorerFrame!.Navigate(typeof(Explorer), mainWindow);
            mainWindow!.Menu.SelectedItem = mainWindow.ItemExplorer;
            mainWindow.editorFrame.Navigate(typeof(Editor), mainWindow);
            mainWindow.OpenExplorer.IsEnabled = true;
            mainWindow.SideMenuEditorArea.ColumnDefinitions[0].Width = new GridLength(360);
        }

        private void Directories_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if ((Directories)Directories.SelectedValue == null)
                return;
            DirPath.Text = ((Directories)Directories.SelectedValue).Path;
        }

        private void OpenCloseButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            mainWindow!.editorFrame.Navigate(typeof(Editor), mainWindow);
        }
    }
}
