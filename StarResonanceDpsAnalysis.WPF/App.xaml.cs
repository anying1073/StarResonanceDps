using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharpPcap;
using StarResonanceDpsAnalysis.WPF.Data;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.Settings;
using StarResonanceDpsAnalysis.WPF.Themes;
using StarResonanceDpsAnalysis.WPF.ViewModels;
using StarResonanceDpsAnalysis.WPF.Views;

namespace StarResonanceDpsAnalysis.WPF;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static ILogger<App>? _logger;
    private static IHost? _host;

    [STAThread]
    private static void Main(string[] args)
    {
        // 创建主机
        _host = CreateHostBuilder(args).Build();
        _logger = _host.Services.GetService<ILogger<App>>();

        App app = new();
        app.InitializeComponent();
        app.MainWindow = _host.Services.GetRequiredService<MainWindow>();
        app.MainWindow.Visibility = Visibility.Visible;
        app.Run();
    }

    /// <summary>
    /// Get the current host instance for accessing services
    /// </summary>
    public static IHost? Host => _host;

    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        var ret =
            Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, builder) =>
                {
                    builder.SetBasePath(AppContext.BaseDirectory);
                    // 读取配置文件
                    builder.AddConfiguration(new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json", reloadOnChange: true, optional: false)
                        .AddJsonFile("appsettings.Development.json", true)
                        .Build());
                })
                // 注册服务
                .ConfigureServices((context, services) =>
                {
                    // 配置 JSON 序列化选项，添加 ModifierKeys 转换器
                    services.AddJsonConfiguration();

                    // 注册配置服务 - 使用Options Pattern
                    services.Configure<AppConfig>(context.Configuration.GetSection("Config"));

                    // 注册视图和视图模型
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<MainWindow>();
                    services.AddTransient<DpsStatisticsViewModel>();
                    services.AddTransient<DpsStatisticsView>();
                    services.AddTransient<SettingsViewModel>();
                    services.AddTransient<SettingsView>();
                    services.AddSingleton<DebugFunctions>();
                    services.AddSingleton<CaptureDeviceList>(CaptureDeviceList.Instance);
                    services.AddThemes();
                    services.AddSingleton<IApplicationController, ApplicationController>();
                    services.AddSingleton<IDataSource, DpsDummyDataSource>();
                    services.AddSingleton<IDeviceManager, DeviceManager>();
                    services.AddDataStorage();

                    // 注册配置管理器
                    services.AddSingleton<IConfigManager, ConfigManger>();

                    // 注册主线程调度器
                    services.AddSingleton(_ => Current.Dispatcher);
                })
                .ConfigureLogging(config => { config.AddConsole(); });
        return ret;
    }
}