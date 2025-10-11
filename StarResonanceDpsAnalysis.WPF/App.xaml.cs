using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using SharpPcap;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Data;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.Themes;
using StarResonanceDpsAnalysis.WPF.ViewModels;
using StarResonanceDpsAnalysis.WPF.Views;
using AppConfig = StarResonanceDpsAnalysis.WPF.Config.AppConfig;

namespace StarResonanceDpsAnalysis.WPF;

public partial class App : Application
{
    private static ILogger<App>? _logger;
    private static IObservable<LogEvent>? _logStream; // exposed for UI subscription

    public static IHost? Host { get; private set; }

    [STAThread]
    private static void Main(string[] args)
    {
        var configRoot = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", false, true)
            .AddJsonFile("appsettings.Development.json", true, true)
            .Build();

        IObservable<LogEvent>? streamRef = null;
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configRoot)
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Observers(obs => streamRef = obs) // capture observable
            // .WriteTo.File()
            .CreateLogger();
        _logStream = streamRef;

        Host = CreateHostBuilder(args, configRoot).Build();
        _logger = Host.Services.GetRequiredService<ILogger<App>>();

        _logger.LogInformation("Application starting");

        App app = new();
        app.InitializeComponent();
        var appOptions = Host.Services.GetRequiredService<IOptions<AppConfig>>();
        LocalizationManager.Initialize(appOptions.Value.Language);
        var analyzer = Host.Services.GetRequiredService<IPacketAnalyzer>();
        app.MainWindow = Host.Services.GetRequiredService<MainWindow>();
        app.MainWindow.Visibility = Visibility.Visible;
        analyzer.Start();
        app.Run();
        analyzer.Stop();

        _logger.LogInformation("Application exiting");
        Log.CloseAndFlush();
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
                services.AddPacketAnalyzer();
                services.AddSingleton<IConfigManager, ConfigManger>();
                if (_logStream != null) services.AddSingleton<IObservable<LogEvent>>(_logStream);
                services.AddSingleton(_ => Current.Dispatcher);
            })
            .ConfigureLogging(lb => lb.ClearProviders());
    }
}