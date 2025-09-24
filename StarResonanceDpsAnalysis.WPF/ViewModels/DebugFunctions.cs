using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Serilog.Events;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.WPF.Data;
using StarResonanceDpsAnalysis.WPF.Views;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class DebugFunctions : BaseViewModel, IDisposable
{
    private readonly DpsStatisticsViewModel _dpsStatisticsViewModel;
    private readonly IDataSource _dataSource;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<DebugFunctions> _logger;
    private readonly PacketAnalyzer _packetAnalyzer = new();
    private CancellationTokenSource? _replayCts;
    private Task? _replayTask;
    private readonly IDisposable? _logSubscription;

    [ObservableProperty] private ObservableCollection<LogEntry> _logs = new();
    [ObservableProperty] private ICollectionView? _filteredLogs;
    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private LogLevel _selectedLogLevel = LogLevel.Trace;
    [ObservableProperty] private bool _autoScrollEnabled = true;
    [ObservableProperty] private int _logCount;
    [ObservableProperty] private int _filteredLogCount;
    [ObservableProperty] private DateTime? _lastLogTime;

    public LogLevel[] AvailableLogLevels { get; } =
    [
        LogLevel.Trace, LogLevel.Debug, LogLevel.Information,
        LogLevel.Warning, LogLevel.Error, LogLevel.Critical
    ];

    public event EventHandler? LogAdded;

    public DebugFunctions(
        DpsStatisticsViewModel dpsStatisticsViewModel,
        IDataSource dataSource,
        Dispatcher dispatcher,
        ILogger<DebugFunctions> logger, IObservable<LogEvent> observer)
    {
        _dpsStatisticsViewModel = dpsStatisticsViewModel;
        _dataSource = dataSource;
        _dispatcher = dispatcher;
        _logger = logger;

        _logSubscription = observer.Subscribe(OnSerilogEvent);

        FilteredLogs = CollectionViewSource.GetDefaultView(Logs);
        FilteredLogs.Filter = LogFilter;
        PropertyChanged += OnPropertyChanged;

        _logger.LogInformation("DebugFunctions initialized with Serilog observable sink");
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

        var sourceContext = evt.Properties.TryGetValue("SourceContext", out var sc) ? sc.ToString().Trim('"') : string.Empty;
        var rendered = evt.RenderMessage();

        var entry = new LogEntry
        {
            Timestamp = evt.Timestamp.LocalDateTime,
            Level = mappedLevel,
            Category = sourceContext,
            Message = rendered,
            Exception = evt.Exception
        };

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

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(FilterText) or nameof(SelectedLogLevel))) return;
        FilteredLogs?.Refresh();
        UpdateFilteredLogCount();
    }

    private bool LogFilter(object item)
    {
        if (item is not LogEntry log) return false;
        if (log.Level < SelectedLogLevel) return false;
        if (string.IsNullOrWhiteSpace(FilterText)) return true;
        return log.Message.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
               log.Category.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateFilteredLogCount() => FilteredLogCount = FilteredLogs?.Cast<object>().Count() ?? 0;

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

    #region AddData
    [RelayCommand]
    private void AddSampleData()
    {
        _dpsStatisticsViewModel.AddRandomData();
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
        StartPcapReplay(dlg.FileName, true, 1.0);
        _logger.LogInformation("Started replaying PCAP file: {File}", Path.GetFileName(dlg.FileName));
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
                try { _replayCts?.Dispose(); }
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
        catch (AggregateException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping PCAP replay");
        }
        finally
        {
            try { _replayCts.Dispose(); }
            catch
            {
                // ignored
            }

            _replayCts = null;
            _replayTask = null;
        }
    }

    #endregion

    public void Dispose()
    {
        _logSubscription?.Dispose();
    }
}