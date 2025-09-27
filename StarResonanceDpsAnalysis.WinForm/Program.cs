using System.Text;
using System.Windows.Forms;

using StarResonanceDpsAnalysis.WinForm.Forms;

namespace StarResonanceDpsAnalysis.WinForm
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                // 记录日志
                Log("UnhandledException", (Exception)e.ExceptionObject);
            };
            Application.ThreadException += (sender, e) =>
            {
                // 记录日志
                Log("ThreadException", e.Exception);
            };

            ApplicationConfiguration.Initialize();

            Application.Run(FormManager.DpsStatistics);
        }

        static async void Log(string type, Exception ex)
        {
            var path = Path.Combine(Environment.CurrentDirectory, "Log");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var fileName = $"{DateTime.Now:yyyy_MM_dd}.log";
            using var sw = new StreamWriter(Path.Combine(path, fileName), new FileStreamOptions
            {
                Access = FileAccess.Write,
                Mode = FileMode.OpenOrCreate,
            });
            var d = DateTime.UtcNow;
            await sw.WriteLineAsync(
                $"""
                [(UTC) {d:HH:mm:ss.fff}] ({type}) 
                {ex.Message}
                {ex.StackTrace}

                """);

        }
    }
}