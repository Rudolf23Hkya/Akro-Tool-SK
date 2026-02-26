using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wpf-binding-errors.log");

        var listener = new TextWriterTraceListener(logPath);
        listener.TraceOutputOptions = TraceOptions.DateTime;

        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;

        // Optional: also capture Debug.WriteLine / Trace.WriteLine
        Trace.Listeners.Add(listener);

        Trace.AutoFlush = true;
    }
}