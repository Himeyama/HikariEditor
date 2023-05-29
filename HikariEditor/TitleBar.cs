using HikariEditor;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using WinRT;
using WinRT.Interop;

namespace Svgicon5
{

    class TitleBar
    {
        private AppWindow mAppWindow;
        MainWindow mainWindow;
        WindowsSystemDispatcherQueueHelper mWsdqHelper;
        MicaController mBackdropController;
        SystemBackdropConfiguration mConfigurationSource;
        public TitleBar(MainWindow mainWindow, string title)
        {
            this.mainWindow = mainWindow;
            mAppWindow = GetAppWindowForCurrentWindow();
            mAppWindow.Title = title;
            mainWindow.ExtendsContentIntoTitleBar = true;
            mainWindow.SetTitleBar(mainWindow.AppTitleBar);
            TrySetSystemBackdrop();
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(mainWindow);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(wndId);
        }

        bool TrySetSystemBackdrop()
        {
            if (MicaController.IsSupported())
            {
                mWsdqHelper = new();
                mWsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                mConfigurationSource = new SystemBackdropConfiguration();
                mainWindow.Activated += Window_Activated;
                mainWindow.Closed += Window_Closed;
                ((FrameworkElement)mainWindow.Content).ActualThemeChanged += Window_ThemeChanged;

                mConfigurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                mBackdropController = new MicaController();

                mBackdropController.AddSystemBackdropTarget(mainWindow.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                mBackdropController.SetSystemBackdropConfiguration(mConfigurationSource);
                return true;
            }
            return false;
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            mConfigurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            if (mBackdropController != null)
            {
                mBackdropController.Dispose();
                mBackdropController = null;
            }
            mainWindow.Activated -= Window_Activated;
            mConfigurationSource = null;
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (mConfigurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            switch (((FrameworkElement)mainWindow.Content).ActualTheme)
            {
                case ElementTheme.Dark: mConfigurationSource.Theme = SystemBackdropTheme.Dark; break;
                case ElementTheme.Light: mConfigurationSource.Theme = SystemBackdropTheme.Light; break;
                case ElementTheme.Default: mConfigurationSource.Theme = SystemBackdropTheme.Default; break;
            }
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
            return;
        }

        if (m_dispatcherQueueController == null)
        {
            DispatcherQueueOptions options;
            options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
            options.threadType = 2;
            options.apartmentType = 2;

            CreateDispatcherQueueController(options, ref m_dispatcherQueueController);
        }
    }
}
