using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using Serilog.Events;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Views;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class DebugFunctions : BaseViewModel, IDisposable
{
    private const int MaxLogEntries = 2000; // allow more lines for context
    private const int FilterDebounceMs = 250;
    private const int BatchSize = 20; // Process logs in larger batches

    private readonly Dispatcher _dispatcher;
    private readonly LocalizationManager _localizationManager;
    private readonly IDataStorage _storage;
    private readonly ILogger<DebugFunctions> _logger;
    private readonly IDisposable? _logSubscription;
    private readonly IPacketAnalyzer _packetAnalyzer;
    private readonly Queue<LogEntry> _pendingLogs = new();
    [ObservableProperty] private bool _autoScrollEnabled = true;

    [ObservableProperty] private List<Option<Language>> _availableLanguages =
    [
        new(Language.Auto, Language.Auto.GetLocalizedDescription()),
        new(Language.ZhCn, Language.ZhCn.GetLocalizedDescription()),
        new(Language.EnUs, Language.EnUs.GetLocalizedDescription()),
        new(Language.PtBr, Language.PtBr.GetLocalizedDescription()),
        new(Language.KoKr, Language.KoKr.GetLocalizedDescription())
    ];

    [ObservableProperty] private bool _enabled;
    private Timer? _filterDebounceTimer;
    [ObservableProperty] private int _filteredLogCount;
    [ObservableProperty] private ICollectionView? _filteredLogs;
    [ObservableProperty] private string _filterText = string.Empty;
    private volatile bool _isBatchProcessing;
    [ObservableProperty] private DateTime? _lastLogTime;
    [ObservableProperty] private int _logCount;

    [ObservableProperty] private ObservableCollection<LogEntry> _logs = new();
    private CancellationTokenSource? _replayCts;
    private Task? _replayTask;
    [ObservableProperty] private Option<Language>? _selectedLanguage;
    [ObservableProperty] private LogLevel _selectedLogLevel = LogLevel.Information;

    public DebugFunctions(Dispatcher dispatcher,
        ILogger<DebugFunctions> logger,
        IObservable<LogEvent> observer,
        IOptionsMonitor<AppConfig> options,
        IPacketAnalyzer packetAnalyzer,
        LocalizationManager localizationManager,
        IDataStorage storage)
    {
        _dispatcher = dispatcher;
        _logger = logger;

        _logSubscription = observer.Subscribe(OnSerilogEvent);

        FilteredLogs = CollectionViewSource.GetDefaultView(Logs);
        FilteredLogs.Filter = LogFilter;
        PropertyChanged += OnPropertyChanged;
        SetProperty(options.CurrentValue, null);
        options.OnChange(SetProperty);
        _packetAnalyzer = packetAnalyzer;
        _localizationManager = localizationManager;
        _storage = storage;

        _logger.LogInformation("Debug panel initialized");
    }

    public LogLevel[] AvailableLogLevels { get; } =
    [
        LogLevel.Trace, LogLevel.Debug, LogLevel.Information,
        LogLevel.Warning, LogLevel.Error, LogLevel.Critical
    ];

    public void Dispose()
    {
        _filterDebounceTimer?.Dispose();
        _logSubscription?.Dispose();

        // Clear any pending logs
        lock (_pendingLogs)
        {
            _pendingLogs.Clear();
        }
    }

    public event EventHandler? LogAdded;

    // Event to request sample data addition - removes direct dependency on DpsStatisticsViewModel
    public event EventHandler? SampleDataRequested;

    partial void OnSelectedLanguageChanged(Option<Language>? value)
    {
        if (value == null) return;
        _localizationManager.ApplyLanguage(value.Value);
    }

    private void SetProperty(AppConfig arg1, string? arg2)
    {
        Enabled = arg1.DebugEnabled;
    }

    private void OnSerilogEvent(LogEvent evt)
    {
        var mappedLevel = evt.Level switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Fatal => LogLevel.Critical,
            _ => LogLevel.Information
        };

        var sourceContext = evt.Properties.TryGetValue("SourceContext", out var sc)
            ? sc.ToString().Trim('"')
            : string.Empty;
        var rendered = evt.RenderMessage();
        var timestamp = evt.Timestamp.LocalDateTime;

        var entry = new LogEntry(timestamp, mappedLevel, rendered, sourceContext, evt.Exception);

        // Add to pending queue for batch processing
        lock (_pendingLogs)
        {
            _pendingLogs.Enqueue(entry);
        }

        // Trigger batch processing if not already running
        if (!_isBatchProcessing)
        {
            _isBatchProcessing = true;
            _dispatcher.BeginInvoke(ProcessLogBatch, DispatcherPriority.Background);
        }
    }

    private void ProcessLogBatch()
    {
        try
        {
            var processedCount = 0;
            var shouldRefresh = false;
            LogEntry? lastEntry = null;

            // Process logs in batches
            lock (_pendingLogs)
            {
                while (_pendingLogs.Count > 0 && processedCount < BatchSize)
                {
                    var entry = _pendingLogs.Dequeue();

                    // Remove oldest entries if we're at the limit
                    while (Logs.Count >= MaxLogEntries)
                    {
                        Logs.RemoveAt(0);
                    }

                    Logs.Add(entry);
                    lastEntry = entry;
                    processedCount++;

                    // Check if this entry would be visible after filtering
                    if (LogFilter(entry))
                    {
                        shouldRefresh = true;
                    }
                }
            }

            // Update properties only once per batch
            if (processedCount > 0)
            {
                LogCount = Logs.Count;
                if (lastEntry != null)
                {
                    LastLogTime = lastEntry.Timestamp;
                }

                if (shouldRefresh)
                {
                    FilteredLogs?.Refresh();
                    UpdateFilteredLogCount();
                    LogAdded?.Invoke(this, EventArgs.Empty);
                }
            }

            // Continue processing if there are more logs
            lock (_pendingLogs)
            {
                if (_pendingLogs.Count > 0)
                {
                    _dispatcher.BeginInvoke(ProcessLogBatch, DispatcherPriority.Background);
                }
                else
                {
                    _isBatchProcessing = false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing log batch");
            _isBatchProcessing = false;
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(FilterText):
                // Debounce filter text changes
                _filterDebounceTimer?.Dispose();
                _filterDebounceTimer = new Timer(_ =>
                {
                    _dispatcher.BeginInvoke(() =>
                    {
                        FilteredLogs?.Refresh();
                        UpdateFilteredLogCount();
                    }, DispatcherPriority.Background);
                }, null, FilterDebounceMs, Timeout.Infinite);
                break;
            case nameof(SelectedLogLevel):
                FilteredLogs?.Refresh();
                UpdateFilteredLogCount();
                break;
        }
    }

    private bool LogFilter(object item)
    {
        if (item is not LogEntry log) return false;
        if (log.Level < SelectedLogLevel) return false;
        if (string.IsNullOrWhiteSpace(FilterText)) return true;
        return log.Message.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
               log.Category.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateFilteredLogCount()
    {
        FilteredLogCount = FilteredLogs?.Cast<object>().Count() ?? 0;
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
        // Clear pending logs as well
        lock (_pendingLogs)
        {
            _pendingLogs.Clear();
        }

        Logs.Clear();
        LogCount = 0;
        FilteredLogCount = 0;
        LastLogTime = null;
        _logger.LogInformation("Logs cleared");
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
                writer.WriteLine(
                    $"[{log.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{log.Level}] [{log.Category}] {log.Message}");
                if (log.Exception != null) writer.WriteLine($"Exception: {log.Exception}");
            }

            _logger.LogInformation("Logs saved to {File}", dlg.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save logs to {File}", dlg.FileName);
        }
    }

    [RelayCommand]
    private void AddTestLog()
    {
        _logger.LogInformation("Test log entry {Id}", Guid.NewGuid().ToString("N")[..8]);
    }

    [RelayCommand]
    private void ClearPlayerInfoCache()
    {
        _storage.ClearPlayerInfos();
    }

    #region Localization

    [ObservableProperty] private string _testLocalizationKey = string.Empty;
    [ObservableProperty] private string _localizedTestValue = string.Empty;

    [RelayCommand]
    private void TestLocalization()
    {
        var ret = _localizationManager.GetString(TestLocalizationKey);
        LocalizedTestValue = ret;
    }

    #endregion

    #region AddData

    [RelayCommand]
    private void AddSampleData()
    {
        // Fire event instead of directly calling DpsStatisticsViewModel
        SampleDataRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

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
        StartPcapReplay(dlg.FileName);
        _logger.LogInformation("Replaying PCAP: {File}", Path.GetFileName(dlg.FileName));
    }

    private void StartPcapReplay(string filePath, bool realtime = true, double speed = 1.0)
    {
        StopPcapReplay();
        _replayCts = new CancellationTokenSource();
        var token = _replayCts.Token;
        _replayTask = Task.Run(async () =>
        {
            try
            {
                await _packetAnalyzer.ReplayFileAsync(filePath, realtime, speed, token).ConfigureAwait(false);
                _logger.LogInformation("PCAP replay completed: {File}", Path.GetFileName(filePath));
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("PCAP replay cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PCAP replay failed: {File}", Path.GetFileName(filePath));
            }
            finally
            {
                try
                {
                    _replayCts?.Dispose();
                }
                catch
                {
                    // ignored
                }

                _replayCts = null;
                _replayTask = null;
            }
        }, token);
    }

    private void StopPcapReplay()
    {
        if (_replayCts == null) return;
        try
        {
            _replayCts.Cancel();
            _replayTask?.Wait(3000);
            _logger.LogInformation("PCAP replay stopped");
        }
        catch (AggregateException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping PCAP replay");
        }
        finally
        {
            try
            {
                _replayCts.Dispose();
            }
            catch
            {
                // ignored
            }

            _replayCts = null;
            _replayTask = null;
        }
    }

    #endregion
}
