using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using SharpPcap;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Data;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.Themes;
using StarResonanceDpsAnalysis.WPF.ViewModels;
using StarResonanceDpsAnalysis.WPF.Views;
using AppConfig = StarResonanceDpsAnalysis.WPF.Config.AppConfig;

namespace StarResonanceDpsAnalysis.WPF;

public partial class App : Application
{
    private static ILogger<App>? _logger;
    private static IHost? _host;
    private static IObservable<LogEvent>? _logStream; // exposed for UI subscription

    [STAThread]
    private static void Main(string[] args)
    {
        var configRoot = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        IObservable<LogEvent>? streamRef = null;
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configRoot)
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Observers(obs => streamRef = obs) // capture observable
            .CreateLogger();
        _logStream = streamRef;

        _host = CreateHostBuilder(args, configRoot).Build();
        _logger = _host.Services.GetRequiredService<ILogger<App>>();

        Log.Information("Application starting");

        App app = new();
        app.InitializeComponent();
        app.MainWindow = _host.Services.GetRequiredService<MainWindow>();
        app.MainWindow.Visibility = Visibility.Visible;
        app.Run();

        Log.Information("Application exiting");
        Log.CloseAndFlush();
    }

    public static IHost? Host => _host;

    private static IHostBuilder CreateHostBuilder(string[] args, IConfiguration configRoot)
    {
        return Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(builder =>
            {
                builder.AddConfiguration(configRoot);
            })
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
                services.AddSingleton<DebugFunctions>();
                services.AddSingleton<CaptureDeviceList>(CaptureDeviceList.Instance);
                services.AddThemes();
                services.AddWindowManagementService();
                services.AddSingleton<IApplicationController, ApplicationController>();
                services.AddSingleton<IDataSource, DpsDummyDataSource>();
                services.AddSingleton<IDeviceManager, DeviceManager>();
                services.AddDataStorage();
                services.AddSingleton<IConfigManager, ConfigManger>();
                if (_logStream != null) services.AddSingleton<IObservable<LogEvent>>(_logStream);
                services.AddSingleton(_ => Current.Dispatcher);
            })
            .ConfigureLogging(lb => lb.ClearProviders());
    }
}