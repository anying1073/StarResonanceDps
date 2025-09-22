using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Views
{
    /// <summary>
    /// Interaction logic for DebugView.xaml
    /// </summary>
    public partial class DebugView : Window
    {
        public DebugView(DebugFunctions debugFunctions)
        {
            InitializeComponent();
            DataContext = debugFunctions;
            
            // Auto scroll when new logs are added
            debugFunctions.LogAdded += OnLogAdded;
        }

        private void OnLogAdded(object? sender, EventArgs e)
        {
            if (DataContext is DebugFunctions debugFunctions && debugFunctions.AutoScrollEnabled)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (FindName("LogScrollViewer") is ScrollViewer scrollViewer)
                    {
                        scrollViewer.ScrollToEnd();
                    }
                }));
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is DebugFunctions debugFunctions)
            {
                debugFunctions.LogAdded -= OnLogAdded;
            }
            base.OnClosed(e);
        }
    }

    /// <summary>
    /// Converter for log level to brush color
    /// </summary>
    public class LogLevelToBrushConverter : IValueConverter
    {
        public static readonly LogLevelToBrushConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Trace => new SolidColorBrush(Colors.Gray),
                    LogLevel.Debug => new SolidColorBrush(Colors.DarkBlue),
                    LogLevel.Information => new SolidColorBrush(Colors.Green),
                    LogLevel.Warning => new SolidColorBrush(Colors.Orange),
                    LogLevel.Error => new SolidColorBrush(Colors.Red),
                    LogLevel.Critical => new SolidColorBrush(Colors.DarkRed),
                    _ => new SolidColorBrush(Colors.Black)
                };
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter for log level to background color (for highlighting critical messages)
    /// </summary>
    public class LogLevelToBackgroundConverter : IValueConverter
    {
        public static readonly LogLevelToBackgroundConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Error => new SolidColorBrush(Color.FromRgb(255, 245, 245)), // Light red
                    LogLevel.Critical => new SolidColorBrush(Color.FromRgb(255, 235, 235)), // Lighter red
                    LogLevel.Warning => new SolidColorBrush(Color.FromRgb(255, 250, 240)), // Light orange
                    _ => Brushes.Transparent
                };
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter for exception to string (for tooltips)
    /// </summary>
    public class ExceptionToStringConverter : IValueConverter
    {
        public static readonly ExceptionToStringConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Exception ex)
            {
                return $"Exception: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
            }
            return null!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
