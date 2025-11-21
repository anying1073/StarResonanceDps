using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using SharpPcap;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Logging;
using StarResonanceDpsAnalysis.WPF.Plugins;
using StarResonanceDpsAnalysis.WPF.Plugins.BuiltIn;
using StarResonanceDpsAnalysis.WPF.Plugins.Interfaces;
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

        _logger.LogInformation(WpfLogEvents.AppStarting, "Application starting");

        var app = new App();
        app.InitializeComponent();

        // General exception handler for unhandled exceptions
        app.DispatcherUnhandledException += (sender, e) =>
        {
            _logger?.LogError(e.Exception, "Unhandled dispatcher exception");
            // Optionally handle gracefully
            // e.Handled = true;
        };

        // Centralized application startup (localization, adapter, analyzer)
        var appStartup = Host.Services.GetRequiredService<IApplicationStartup>();
        appStartup.InitializeAsync().Wait();

        app.MainWindow = Host.Services.GetRequiredService<MainView>();
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

        _logger.LogInformation(WpfLogEvents.AppExiting, "Application exiting");
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

                RegisterViewModels(services);
                RegisterViews(services);

                services.AddPacketAnalyzer();
                services.AddThemes();
                services.AddWindowManagementService();
                services.AddMessageDialogService();

                services.AddSingleton<DebugFunctions>();
                services.AddSingleton(CaptureDeviceList.Instance);
                services.AddSingleton<IApplicationControlService, ApplicationControlService>();
                services.AddSingleton<IDeviceManagementService, DeviceManagementService>();
                services.AddSingleton<IApplicationStartup, ApplicationStartup>();
                services.AddSingleton<IConfigManager, ConfigManger>();
                services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
                services.AddSingleton<IMousePenetrationService, MousePenetrationService>();
                services.AddSingleton<ITopmostService, TopmostService>();

                RegisterBuiltInPlugins(services);

                services.AddSingleton<IPluginManager, PluginManager>();
                services.AddSingleton<ITrayService, TrayService>();

                if (_logStream != null) services.AddSingleton(_logStream);

                services.AddSingleton(_ => Current.Dispatcher);

                // Localization manager singleton
                services.AddSingleton(new LocalizationConfiguration
                {
                    LocalizationDirectory = Path.Combine(AppContext.BaseDirectory, "Data")
                });
                services.AddSingleton<LocalizationManager>();
            })
            .ConfigureLogging(lb => lb.ClearProviders());
    }

    static readonly Dictionary<Type, ServiceLifetime> LifeTimeOverrides = new()
    {
        { typeof(DpsStatisticsViewModel), ServiceLifetime.Singleton },
        { typeof(DpsStatisticsView), ServiceLifetime.Transient },
        { typeof(SkillBreakdownViewModel), ServiceLifetime.Transient },
        { typeof(SkillBreakdownView), ServiceLifetime.Transient },
    };

    private static void RegisterViewModels(IServiceCollection services)
    {
        RegisterTypes(services, "StarResonanceDpsAnalysis.WPF.ViewModels", "ViewModel");
    }

    private static void RegisterViews(IServiceCollection services)
    {
        RegisterTypes(services, "StarResonanceDpsAnalysis.WPF.Views", "View");
    }

    private static void RegisterBuiltInPlugins(IServiceCollection services) 
    {
        RegisterTypes(services, "StarResonanceDpsAnalysis.WPF.Plugins.BuiltIn", "Plugin", typeof(IPlugin), ServiceLifetime.Singleton);
    }

    private static void RegisterTypes(
        IServiceCollection services,
        string @namespace,
        string suffix,
        Type? overrideServiceType = null,
        ServiceLifetime defLifetime = ServiceLifetime.Transient)
    {
        var types = typeof(App).Assembly
            .GetTypes()
            .Where(t =>
                t is { IsAbstract: false, IsClass: true } &&
                t.Namespace != null &&
                t.Namespace.StartsWith(@namespace, StringComparison.Ordinal) &&
                t.Name.EndsWith(suffix, StringComparison.Ordinal));

        foreach (var type in types)
        {
            var lifetime = LifeTimeOverrides.TryGetValue(type, out var overrideLifetime)
                ? overrideLifetime
                : defLifetime;

            var serviceType = overrideServiceType ?? type;

            services.Add(new ServiceDescriptor(serviceType, type, lifetime));
        }
    }
}
