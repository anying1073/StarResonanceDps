using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.WPF.Logging;

namespace StarResonanceDpsAnalysis.WPF.Services;

public sealed class TrayService : ITrayService, IDisposable
{
    private TaskbarIcon? _tray;
    private bool _initialized;
    private readonly ILogger<TrayService>? _logger;

    public TrayService(ILogger<TrayService>? logger = null)
    {
        _logger = logger;
    }

    public void Initialize(string? toolTip = null)
    {
        if (_initialized) return;
        _initialized = true;

        _tray = new TaskbarIcon
        {
            ToolTipText = toolTip ?? Application.Current?.MainWindow?.Title ?? "App",
            Visibility = Visibility.Visible
        };

        _tray.IconSource = LoadTrayIcon();

        var menu = new ContextMenu();
        var miShow = new MenuItem { Header = "Show" };
        miShow.Click += (_, _) => Restore();
        var miExit = new MenuItem { Header = "Exit" };
        miExit.Click += (_, _) => Exit();
        menu.Items.Add(miShow);
        menu.Items.Add(new Separator());
        menu.Items.Add(miExit);
        _tray.ContextMenu = menu;
        _tray.TrayMouseDoubleClick += (_, _) => Restore();

        _logger?.LogInformation(WpfLogEvents.TrayInit, "Tray initialized");
    }

    public void MinimizeToTray()
    {
        var main = Application.Current?.MainWindow;
        if (main == null) return;
        main.Hide();
        _logger?.LogDebug(WpfLogEvents.TrayMinimize, "Window minimized to tray");
    }

    public void Restore()
    {
        var main = Application.Current?.MainWindow;
        if (main == null) return;
        main.Show();
        if (main.WindowState == WindowState.Minimized) main.WindowState = WindowState.Normal;
        main.Activate();
        _logger?.LogDebug(WpfLogEvents.TrayRestore, "Window restored from tray");
    }

    public void Exit()
    {
        _logger?.LogInformation(WpfLogEvents.TrayExit, "Tray exit requested");
        Dispose();
        Application.Current?.Shutdown();
    }

    public void Dispose()
    {
        try { _tray?.Dispose(); }
        catch
        {
            // Ignore
        }
        _tray = null;
    }

    private static ImageSource? LoadTrayIcon()
    {
        try
        {
            var iconUri = new Uri("pack://application:,,,/Assets/Images/ApplicationIcon.ico");
            var streamInfo = Application.GetResourceStream(iconUri);
            if (streamInfo == null)
            {
                return null;
            }

            using var iconStream = streamInfo.Stream;
            var decoder = new IconBitmapDecoder(
                iconStream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count == 0)
            {
                return null;
            }

            var desiredSize = GetTrayIconSize();
            return decoder.Frames
                .OrderBy(f => Math.Abs(f.PixelWidth - desiredSize))
                .ThenByDescending(f => f.Format.BitsPerPixel)
                .ThenByDescending(f => f.PixelWidth)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static int GetTrayIconSize()
    {
        const int defaultSize = 16;
        var window = Application.Current?.MainWindow;
        if (window == null)
        {
            return defaultSize;
        }

        try
        {
            var dpi = VisualTreeHelper.GetDpi(window);
            return Math.Max(defaultSize, (int)Math.Round(defaultSize * dpi.DpiScaleX));
        }
        catch
        {
            return defaultSize;
        }
    }
}
