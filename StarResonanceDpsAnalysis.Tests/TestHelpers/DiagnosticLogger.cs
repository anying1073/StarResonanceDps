using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace StarResonanceDpsAnalysis.Tests;

/// <summary>
/// Logger implementation that writes to xUnit test output
/// </summary>
public class DiagnosticLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public DiagnosticLogger(ITestOutputHelper output, string categoryName)
    {
        _output = output;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        var logMessage = $"[{logLevel}] {_categoryName}: {message}";
        
        if (exception != null)
        {
            logMessage += $"\n{exception}";
        }

        try
        {
            _output.WriteLine(logMessage);
        }
        catch
        {
            // Ignore if output is not available
        }
    }
}

/// <summary>
/// Generic logger implementation that writes to xUnit test output
/// </summary>
public class DiagnosticLogger<T> : ILogger<T>
{
    private readonly DiagnosticLogger _logger;

    public DiagnosticLogger(ITestOutputHelper output, string? categoryName = null)
    {
        _logger = new DiagnosticLogger(output, categoryName ?? typeof(T).Name);
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _logger.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _logger.IsEnabled(logLevel);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logger.Log(logLevel, eventId, state, exception, formatter);
    }
}
