using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.WPF.Data;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.Themes;
using StarResonanceDpsAnalysis.WPF.Views;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// Log entry model for display
/// </summary>
public partial class LogEntry : ObservableObject
{
    [ObservableProperty] private DateTime timestamp;
    [ObservableProperty] private LogLevel level;
    [ObservableProperty] private string message = string.Empty;
    [ObservableProperty] private string category = string.Empty;
    [ObservableProperty] private Exception? exception;
}

/// <summary>
/// Custom logger for capturing logs in memory
/// </summary>
public class MemoryLogger : ILogger
{
    private readonly string _categoryName;
    private readonly Action<LogEntry> _addLogEntry;
    private readonly LogLevel _minLevel;

    public MemoryLogger(string categoryName, Action<LogEntry> addLogEntry, LogLevel minLevel = LogLevel.Trace)
    {
        _categoryName = categoryName;
        _addLogEntry = addLogEntry;
        _minLevel = minLevel;
    }

    public IDisposable BeginScope<TState>(TState state) => new NoOpDisposable();

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = logLevel,
            Message = formatter(state, exception),
            Category = _categoryName,
            Exception = exception
        };

        _addLogEntry(entry);
    }

    private class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }
}

/// <summary>
/// Custom logger provider for capturing logs in memory
/// </summary>
public class MemoryLoggerProvider : ILoggerProvider
{
    private readonly Action<LogEntry> _addLogEntry;
    private readonly LogLevel _minLevel;

    public MemoryLoggerProvider(Action<LogEntry> addLogEntry, LogLevel minLevel = LogLevel.Trace)
    {
        _addLogEntry = addLogEntry;
        _minLevel = minLevel;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new MemoryLogger(categoryName, _addLogEntry, _minLevel);
    }

    public void Dispose() { }
}

public partial class DebugFunctions : BaseViewModel
{
    private readonly DpsStatisticsViewModel _dpsStatisticsViewModel;
    private readonly IDataSource _dataSource;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<DebugFunctions> _logger;

    /// <summary>
    /// 分析器
    /// </summary>
    private readonly PacketAnalyzer _packetAnalyzer = new(); // # 抓包/分析器：每个到达的数据包交由该分析器处理

    private CancellationTokenSource? _replayCts;
    private Task? _replayTask;

    // Log display properties
    [ObservableProperty] private ObservableCollection<LogEntry> logs = new();
    [ObservableProperty] private ICollectionView? filteredLogs;
    [ObservableProperty] private string filterText = string.Empty;
    [ObservableProperty] private LogLevel selectedLogLevel = LogLevel.Trace;
    [ObservableProperty] private bool autoScrollEnabled = true;
    [ObservableProperty] private int logCount;
    [ObservableProperty] private int filteredLogCount;
    [ObservableProperty] private DateTime? lastLogTime;

    // Available log levels for filtering
    public LogLevel[] AvailableLogLevels { get; } = 
    {
        LogLevel.Trace, LogLevel.Debug, LogLevel.Information, 
        LogLevel.Warning, LogLevel.Error, LogLevel.Critical
    };

    // Events
    public event EventHandler? LogAdded;

    public DebugFunctions(
        DpsStatisticsViewModel dpsStatisticsViewModel,
        IDataSource dataSource,
        Dispatcher dispatcher,
        ILogger<DebugFunctions> logger)
    {
        _dpsStatisticsViewModel = dpsStatisticsViewModel;
        _dataSource = dataSource;
        _dispatcher = dispatcher;
        _logger = logger;

        // Initialize filtered view
        FilteredLogs = CollectionViewSource.GetDefaultView(Logs);
        FilteredLogs.Filter = LogFilter;

        // Set up property change handlers
        PropertyChanged += OnPropertyChanged;

        // Add some initial demo logs
        AddDemoLogs();
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FilterText) || e.PropertyName == nameof(SelectedLogLevel))
        {
            FilteredLogs?.Refresh();
            UpdateFilteredLogCount();
        }
    }

    private bool LogFilter(object item)
    {
        if (item is not LogEntry log) return false;

        // Filter by log level
        if (log.Level < SelectedLogLevel) return false;

        // Filter by text content
        if (!string.IsNullOrEmpty(FilterText))
        {
            return log.Message.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                   log.Category.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private void UpdateFilteredLogCount()
    {
        FilteredLogCount = FilteredLogs?.Cast<object>().Count() ?? 0;
    }

    private void AddLogEntry(LogEntry entry)
    {
        _dispatcher.Invoke(() =>
        {
            Logs.Add(entry);
            LogCount = Logs.Count;
            LastLogTime = entry.Timestamp;
            
            FilteredLogs?.Refresh();
            UpdateFilteredLogCount();
            
            LogAdded?.Invoke(this, EventArgs.Empty);
        });
    }

    private void AddDemoLogs()
    {
        AddLogEntry(new LogEntry 
        { 
            Timestamp = DateTime.Now.AddMinutes(-5), 
            Level = LogLevel.Information, 
            Message = "Application started", 
            Category = "Application" 
        });
        
        AddLogEntry(new LogEntry 
        { 
            Timestamp = DateTime.Now.AddMinutes(-4), 
            Level = LogLevel.Debug, 
            Message = "Initializing packet analyzer", 
            Category = "PacketAnalyzer" 
        });
        
        AddLogEntry(new LogEntry 
        { 
            Timestamp = DateTime.Now.AddMinutes(-3), 
            Level = LogLevel.Warning, 
            Message = "No network adapter selected", 
            Category = "NetworkManager" 
        });
        
        AddLogEntry(new LogEntry 
        { 
            Timestamp = DateTime.Now.AddMinutes(-2), 
            Level = LogLevel.Error, 
            Message = "Failed to connect to data source", 
            Category = "DataSource" 
        });
        
        AddLogEntry(new LogEntry 
        { 
            Timestamp = DateTime.Now.AddMinutes(-1), 
            Level = LogLevel.Information, 
            Message = "Ready for packet capture", 
            Category = "Application" 
        });
    }

    [RelayCommand]
    private void CallDebugWindow()
    {
        var debugWindow = new DebugView(this);
        debugWindow.Show();
    }

    [RelayCommand]
    private void ClearLogs()
    {
        Logs.Clear();
        LogCount = 0;
        FilteredLogCount = 0;
        LastLogTime = null;
        
        _logger.LogInformation("Debug logs cleared");
        AddLogEntry(new LogEntry 
        { 
            Timestamp = DateTime.Now, 
            Level = LogLevel.Information, 
            Message = "Debug logs cleared", 
            Category = "DebugView" 
        });
    }

    [RelayCommand]
    private void SaveLogs()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Save Debug Logs",
            FileName = $"debug_logs_{DateTime.Now:yyyyMMdd_HHmmss}.log"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var logsToSave = FilteredLogs?.Cast<LogEntry>() ?? Logs;
            using var writer = new StreamWriter(dlg.FileName);
            
            foreach (var log in logsToSave)
            {
                writer.WriteLine($"[{log.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{log.Level}] [{log.Category}] {log.Message}");
                if (log.Exception != null)
                {
                    writer.WriteLine($"Exception: {log.Exception}");
                }
            }
            
            _logger.LogInformation($"Debug logs saved to {dlg.FileName}");
            AddLogEntry(new LogEntry 
            { 
                Timestamp = DateTime.Now, 
                Level = LogLevel.Information, 
                Message = $"Logs saved to {Path.GetFileName(dlg.FileName)}", 
                Category = "DebugView" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to save logs to {dlg.FileName}");
            AddLogEntry(new LogEntry 
            { 
                Timestamp = DateTime.Now, 
                Level = LogLevel.Error, 
                Message = $"Failed to save logs: {ex.Message}", 
                Category = "DebugView",
                Exception = ex
            });
        }
    }

    [RelayCommand]
    private void AddTestLog()
    {
        var levels = new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical };
        var messages = new[] 
        { 
            "Test message", 
            "Processing packet data", 
            "Connection established", 
            "Memory usage high", 
            "Failed to parse packet", 
            "Critical system error" 
        };
        var categories = new[] { "Test", "PacketAnalyzer", "Connection", "Memory", "Parser", "System" };

        var random = new Random();
        var level = levels[random.Next(levels.Length)];
        var message = messages[random.Next(messages.Length)];
        var category = categories[random.Next(categories.Length)];

        AddLogEntry(new LogEntry 
        { 
            Timestamp = DateTime.Now, 
            Level = level, 
            Message = $"{message} #{random.Next(1000, 9999)}", 
            Category = category 
        });
    }

    #region Replay

    [RelayCommand]
    private void LoadDebugDataSource()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Capture files (*.pcap;*.pcapng)|*.pcap;*.pcapng|All files (*.*)|*.*",
            Title = "Open pcap/pcapng file to replay"
        };

        if (dlg.ShowDialog() != true) return;

        // optional: ask user for realtime/speed settings; here we use defaults
        StartPcapReplay(dlg.FileName, true, 1.0);

        // update UI state (invoke on UI thread)
        _dispatcher.Invoke(() =>
        {
            // show simple feedback
            MessageBox.Show($"Replaying {Path.GetFileName(dlg.FileName)}...", "PCAP Replay", MessageBoxButton.OK,
                MessageBoxImage.Information);
        });

        // Add log entry for pcap loading
        AddLogEntry(new LogEntry 
        { 
            Timestamp = DateTime.Now, 
            Level = LogLevel.Information, 
            Message = $"Started replaying PCAP file: {Path.GetFileName(dlg.FileName)}", 
            Category = "PcapReplay" 
        });
    }

    /// <summary>
    /// Start replaying a pcap/pcapng file into the existing PacketAnalyzer.
    /// Non-blocking: runs on a background task and uses a CancellationToken to stop.
    /// </summary>
    private void StartPcapReplay(string filePath, bool realtime = true, double speed = 1.0)
    {
        // stop any existing replay first
        StopPcapReplay();

        _replayCts = new CancellationTokenSource();
        var token = _replayCts.Token;

        // run replay in background so UI stays responsive
        _replayTask = Task.Run(async () =>
        {
            try
            {
                // PcapReplay.ReplayFileAsync will call PacketAnalyzer.ProcessPacket for each packet
                await PcapReplay.ReplayFileAsync(filePath, _packetAnalyzer, realtime, speed, token)
                    .ConfigureAwait(false);
                
                AddLogEntry(new LogEntry 
                { 
                    Timestamp = DateTime.Now, 
                    Level = LogLevel.Information, 
                    Message = $"PCAP replay completed: {Path.GetFileName(filePath)}", 
                    Category = "PcapReplay" 
                });
            }
            catch (OperationCanceledException)
            {
                // expected on stop
                AddLogEntry(new LogEntry 
                { 
                    Timestamp = DateTime.Now, 
                    Level = LogLevel.Information, 
                    Message = "PCAP replay cancelled", 
                    Category = "PcapReplay" 
                });
            }
            catch (Exception ex)
            {
                // minimal logging; marshal to UI if needed
                _logger.LogDebug($"Pcap replay failed: {ex.Message}");
                AddLogEntry(new LogEntry 
                { 
                    Timestamp = DateTime.Now, 
                    Level = LogLevel.Error, 
                    Message = $"PCAP replay failed: {ex.Message}", 
                    Category = "PcapReplay",
                    Exception = ex
                });
            }
            finally
            {
                // ensure cleanup on completion
                try
                {
                    _replayCts?.Dispose();
                }
                catch
                {
                }

                _replayCts = null;
                _replayTask = null;
            }
        }, token);
    }

    /// <summary>
    /// Stop any running pcap replay (cancels and waits briefly).
    /// </summary>
    private void StopPcapReplay()
    {
        if (_replayCts == null) return;

        try
        {
            _replayCts.Cancel();
            // wait a short time for graceful shutdown; avoid blocking UI thread
            _replayTask?.Wait(3000);
            
            AddLogEntry(new LogEntry 
            { 
                Timestamp = DateTime.Now, 
                Level = LogLevel.Information, 
                Message = "PCAP replay stopped", 
                Category = "PcapReplay" 
            });
        }
        catch (AggregateException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"StopPcapReplay error: {ex.Message}");
            AddLogEntry(new LogEntry 
            { 
                Timestamp = DateTime.Now, 
                Level = LogLevel.Warning, 
                Message = $"Error stopping PCAP replay: {ex.Message}", 
                Category = "PcapReplay" 
            });
        }
        finally
        {
            try
            {
                _replayCts.Dispose();
            }
            catch
            {
            }

            _replayCts = null;
            _replayTask = null;
        }
    }

    #endregion
}

public partial class MainViewModel(
    ApplicationThemeManager themeManager,
    DebugFunctions debugFunctions,
    IWindowManagementService windowManagement) : BaseViewModel
{
    [ObservableProperty]
    private List<ApplicationTheme> _availableThemes =
        [ApplicationTheme.Light, ApplicationTheme.Dark];

    [ObservableProperty] private ApplicationTheme _theme = themeManager.GetAppTheme();
    public DebugFunctions Debug { get; init; } = debugFunctions;

    partial void OnThemeChanged(ApplicationTheme value)
    {
        themeManager.Apply(value);
    }

    [RelayCommand]
    private void CallDpsStatisticsView()
    {
        windowManagement.DpsStatisticsView.Show();
    }

    [RelayCommand]
    private void CallSettingsView()
    {
        windowManagement.SettingsView.Show();
    }

    [RelayCommand]
    private void CallSkillBreakdownView()
    {
        windowManagement.SkillBreakdownView.Show();
    }
}