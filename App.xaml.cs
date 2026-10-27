using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Atletika_SutaznyPlan_Generator
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wpf-binding-errors.log");
            RotateLog(logPath);

            var listener = new TextWriterTraceListener(logPath);
            listener.TraceOutputOptions = TraceOptions.DateTime;

            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;

            // Optional: also capture Debug.WriteLine / Trace.WriteLine
            Trace.Listeners.Add(listener);

            Trace.AutoFlush = true;
        }

        // The log is appended to on every run: above 1 MB keep it as *.old.log (replacing the previous one)
        // and start a new one, so at most ~2 MB stay next to the exe.
        private static void RotateLog(string logPath)
        {
            const long MaxLogBytes = 1024 * 1024;
            try
            {
                var file = new FileInfo(logPath);
                if (file.Exists && file.Length > MaxLogBytes)
                    File.Move(logPath, Path.ChangeExtension(logPath, ".old.log"), overwrite: true);
            }
            catch (IOException)
            {
                // Logging is optional; never block the app start because of it
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}