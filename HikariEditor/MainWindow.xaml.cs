// Licensed under the MIT License.

using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Storage;
using WinRT;

namespace HikariEditor
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        // Mica
        WindowsSystemDispatcherQueueHelper m_wsdqHelper; // See below for implementation.
        MicaController m_backdropController;
        //DesktopAcrylicController m_backdropController;
        SystemBackdropConfiguration m_configurationSource;
        [DllImport("uxtheme.dll", EntryPoint = "#132")]
        private static extern bool ShouldAppsUseDarkMode();
        //Editor editor;
        ApplicationDataContainer config;

        public MainWindow()
        {
            InitializeComponent();
            configSetup();
            loadConfig();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            TrySetSystemBackdrop();

            Manager.mainWindow = this;
            Manager.contentFrame = contentFrame;
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(myWndId);
            appWindow.Resize(new SizeInt32(1920, 1080));
            editorFrame.Navigate(typeof(Editor), this);
        }

        // 開くをクリック
        void OpenClick(object sender, RoutedEventArgs e)
        {
            editorFrame.Navigate(typeof(Open), this);
        }

        void configSetup()
        {
            config = ApplicationData.Current.LocalSettings;
            config.Values["AutoSave"] ??= false;
        }

        void loadConfig()
        {
            AutoSave.IsChecked = (bool)config.Values["AutoSave"];
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            config.Values["AutoSave"] = AutoSaveToggleSwitch.IsOn;
        }
        void EnableAutoSave(object sender, RoutedEventArgs e)
        {
            config.Values["AutoSave"] = AutoSave.IsChecked;
        }

        void ExitClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        void MenuChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            NavigationViewItem selectedItem = args.SelectedItem as NavigationViewItem;
            switch (selectedItem.Tag)
            {
                case "Explorer":
                    contentFrame.Navigate(typeof(Explorer), "hogehogehogehoge");
                    break;
                case "Search":
                    contentFrame.Navigate(typeof(Search));
                    break;
            }
        }

        bool TrySetSystemBackdrop()
        {
            if (DesktopAcrylicController.IsSupported())
            {
                m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                // Create the policy object.
                m_configurationSource = new SystemBackdropConfiguration();
                Activated += Window_Activated;
                Closed += Window_Closed;
                ((FrameworkElement)Content).ActualThemeChanged += Window_ThemeChanged;

                // Initial configuration state.
                m_configurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                //m_backdropController = new MicaController();
                m_backdropController = new();

                m_backdropController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                m_backdropController.SetSystemBackdropConfiguration(m_configurationSource);
                return true; // succeeded
            }

            return false; // Mica is not supported on this system
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Make sure any Mica/Acrylic controller is disposed
            // so it doesn't try to use this closed window.
            if (m_backdropController != null)
            {
                m_backdropController.Dispose();
                m_backdropController = null;
            }
            this.Activated -= Window_Activated;
            m_configurationSource = null;
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (m_configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            switch (((FrameworkElement)this.Content).ActualTheme)
            {
                case ElementTheme.Dark: m_configurationSource.Theme = SystemBackdropTheme.Dark; break;
                case ElementTheme.Light: m_configurationSource.Theme = SystemBackdropTheme.Light; break;
                case ElementTheme.Default: m_configurationSource.Theme = SystemBackdropTheme.Default; break;
            }
        }

        private void ClickNLBtn(object sender, RoutedEventArgs e)
        {
            NLBtn.Content = (string)NLBtn.Content == "LF" ? "CRLF" : "LF";
        }
    }
}


class WindowsSystemDispatcherQueueHelper
{
    [StructLayout(LayoutKind.Sequential)]
    struct DispatcherQueueOptions
    {
        internal int dwSize;
        internal int threadType;
        internal int apartmentType;
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object dispatcherQueueController);

    object m_dispatcherQueueController = null;
    public void EnsureWindowsSystemDispatcherQueueController()
    {
        if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
        {
            // one already exists, so we'll just use it.
            return;
        }

        if (m_dispatcherQueueController == null)
        {
            DispatcherQueueOptions options;
            options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
            options.threadType = 2;    // DQTYPE_THREAD_CURRENT
            options.apartmentType = 2; // DQTAT_COM_STA

            CreateDispatcherQueueController(options, ref m_dispatcherQueueController);
        }
    }
}