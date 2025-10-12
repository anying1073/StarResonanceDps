using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using SharpPcap;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Data;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.Themes;
using StarResonanceDpsAnalysis.WPF.ViewModels;
using StarResonanceDpsAnalysis.WPF.Views;

namespace StarResonanceDpsAnalysis.WPF;

public partial class App : Application
{
    private static ILogger<App>? _logger;
    private static IObservable<LogEvent>? _logStream; // exposed for UI subscription

    public static IHost? Host { get; private set; }

    [STAThread]
    private static void Main(string[] args)
    {
        var configRoot = BuildConfiguration();
        _logStream = ConfigureLogging(configRoot);

        Host = CreateHostBuilder(args, configRoot).Build();
        _logger = Host.Services.GetRequiredService<ILogger<App>>();

        _logger.LogInformation("Application starting");

        var app = new App();
        app.InitializeComponent();

        // Centralized application startup (localization, adapter, analyzer)
        var appStartup = Host.Services.GetRequiredService<IApplicationStartup>();
        appStartup.InitializeAsync().Wait();

        app.MainWindow = Host.Services.GetRequiredService<MainWindow>();
        app.MainWindow.Visibility = Visibility.Visible;
        app.Run();

        // Centralized shutdown
        try
        {
            appStartup.Shutdown();
        }
        catch
        {
            // ignored
        }

        _logger.LogInformation("Application exiting");
        Log.CloseAndFlush();
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", false, true)
            .AddJsonFile("appsettings.Development.json", true, true)
            .Build();
    }

    private static IObservable<LogEvent>? ConfigureLogging(IConfiguration configRoot)
    {
        IObservable<LogEvent>? streamRef = null;
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configRoot)
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Observers(obs => streamRef = obs)
            .CreateLogger();
        return streamRef;
    }

    private static IHostBuilder CreateHostBuilder(string[] args, IConfiguration configRoot)
    {
        return Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(builder => { builder.AddConfiguration(configRoot); })
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                services.AddJsonConfiguration();
                services.Configure<AppConfig>(context.Configuration.GetSection("Config"));
                services.AddTransient<MainViewModel>();
                services.AddTransient<MainWindow>();
                services.AddSingleton<DpsStatisticsViewModel>();
                services.AddSingleton<DpsStatisticsView>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<SettingsView>();
                services.AddTransient<SkillBreakdownViewModel>();
                services.AddTransient<SkillBreakdownView>();
                services.AddTransient<AboutView>();
                services.AddTransient<DamageReferenceView>();
                services.AddTransient<ModuleSolveView>();
                services.AddSingleton<DebugFunctions>();
                services.AddSingleton<CaptureDeviceList>(CaptureDeviceList.Instance);
                services.AddThemes();
                services.AddWindowManagementService();
                services.AddSingleton<IApplicationControlService, ApplicationControlService>();
                services.AddSingleton<IDataSource, DpsDummyDataSource>();
                services.AddSingleton<IDeviceManagementService, DeviceManagementService>();
                services.AddSingleton<IApplicationStartup, ApplicationStartup>();
                services.AddPacketAnalyzer();
                services.AddSingleton<IConfigManager, ConfigManger>();
                if (_logStream != null) services.AddSingleton<IObservable<LogEvent>>(_logStream);
                services.AddSingleton(_ => Current.Dispatcher);
            })
            .ConfigureLogging(lb => lb.ClearProviders());
    }
}