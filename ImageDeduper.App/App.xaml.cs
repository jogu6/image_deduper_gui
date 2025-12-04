using System;
using System.Runtime.InteropServices;
using ImageDeduper.Core.Configuration;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace ImageDeduper.App;

public partial class App : Application
{
    private const int MinWidth = 640;
    private const int MinHeight = 480;
    private AppWindow? _appWindow;
    private SizeInt32 _lastWindowSize;
    private IntPtr _hwnd;
    private IntPtr _originalWndProc;
    private WndProc? _wndProc;

    private delegate IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public static AppSettings Settings { get; } = AppSettings.LoadOrCreate();
    public static Window? MainWindow { get; private set; }

    public App()
    {
        Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (MainWindow is null)
        {
            MainWindow = new Window
            {
                Title = "Image Deduper"
            };
            InitializeWindow(MainWindow);
        }

        MainWindow.Content ??= new Views.MainPage();
        MainWindow.Activate();
    }

    private void InitializeWindow(Window window)
    {
        var desiredWidth = Math.Max(MinWidth, Settings.WindowWidth);
        var desiredHeight = Math.Max(MinHeight, Settings.WindowHeight);
        Settings.WindowWidth = desiredWidth;
        Settings.WindowHeight = desiredHeight;

        var hwnd = WindowNative.GetWindowHandle(window);
        _hwnd = hwnd;
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _lastWindowSize = new SizeInt32(desiredWidth, desiredHeight);
        _appWindow.Resize(_lastWindowSize);

        _wndProc = HandleWindowProc;
        _originalWndProc = SetWindowLongPtr(_hwnd, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProc));

        window.SizeChanged += OnWindowSizeChanged;
        window.Closed += OnWindowClosed;
    }

    private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs e)
    {
        if (e.Size.Width <= 0 || e.Size.Height <= 0)
        {
            return;
        }

        var newWidth = (int)Math.Round(e.Size.Width);
        var newHeight = (int)Math.Round(e.Size.Height);
        _lastWindowSize = new SizeInt32(newWidth, newHeight);
        Settings.WindowWidth = Math.Max(MinWidth, newWidth);
        Settings.WindowHeight = Math.Max(MinHeight, newHeight);
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        var finalSize = _appWindow?.Size ?? _lastWindowSize;
        Settings.WindowWidth = Math.Max(MinWidth, finalSize.Width);
        Settings.WindowHeight = Math.Max(MinHeight, finalSize.Height);
        Settings.Save();

        if (_originalWndProc != IntPtr.Zero)
        {
            SetWindowLongPtr(_hwnd, GWL_WNDPROC, _originalWndProc);
            _originalWndProc = IntPtr.Zero;
            _wndProc = null;
        }
    }

    private IntPtr HandleWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            var info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            info.ptMinTrackSize.X = MinWidth;
            info.ptMinTrackSize.Y = MinHeight;
            Marshal.StructureToPtr(info, lParam, true);
        }

        return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }

    private const int WM_GETMINMAXINFO = 0x0024;
    private const int GWL_WNDPROC = -4;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newProc);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr prevProc, IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }
}
