using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace Atletika_SutaznyPlan_Generator
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // "Akro Tool SK.exe --check-database": write a report of the picture database and exit (no window)
            // (exit before StartupUri opens the main window)
            if (e.Args.Contains("--check-database", StringComparer.OrdinalIgnoreCase))
                Environment.Exit(WriteDatabaseReport());

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

        // database-check.txt next to the exe: pictures per table, missing positions, index.csv problems.
        // Exit code 0 = no problems, 1 = problems found (missing pictures count as problems).
        private static int WriteDatabaseReport()
        {
            var dbRoot = Models.ExerciseImageRepository.ResolveDbRoot();
            var repo = new Models.ExerciseImageRepository(dbRoot);
            var coverage = repo.DescribeCoverage().ToList();

            var lines = new List<string> { $"DataBase: {dbRoot}", "" };
            lines.AddRange(coverage);
            lines.Add("");
            lines.Add(repo.Problems.Count == 0 ? "index.csv: no problems" : "Problems:");
            lines.AddRange(repo.Problems.Select(p => "  " + p));

            File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database-check.txt"), lines);

            var complete = coverage.All(c => !c.Contains("missing:"));
            return complete && repo.Problems.Count == 0 ? 0 : 1;
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