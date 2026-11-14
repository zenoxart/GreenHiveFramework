using System.Collections.ObjectModel;

namespace GreenHive.Wpf.Framework.Mvvm;

/// <summary>
/// Application log (WIFI: ProtokollManager). Entries are bindable; changes are marshalled onto the
/// UI thread through the context's dispatcher. Also writes one file per day to LocalDataPath\Logs.
/// </summary>
public sealed class LogManager : AppObject
{
    private readonly object _fileLock = new();

    /// <summary>Raised after an entry was added.</summary>
    public event EventHandler<LogEntry>? EntryWritten;

    /// <summary>The entries kept in memory (bind a log view to it).</summary>
    public ObservableCollection<LogEntry> Entries { get; } = [];

    /// <summary>Maximum number of entries kept in memory (automatic compaction).</summary>
    public int MaxEntries { get; set; } = 2000;

    /// <summary>Whether entries are also written to a daily log file.</summary>
    public bool WriteToFile { get; set; } = true;

    /// <summary>Writes an entry.</summary>
    public void Write(string text, LogEntryType type = LogEntryType.Information, string? source = null, Exception? exception = null)
    {
        var entry = new LogEntry(DateTimeOffset.Now, type, text, source, exception?.ToString());
        System.Diagnostics.Debug.WriteLine(entry.ToString());
        AppendToFile(entry);

        var dispatcher = HasAppContext ? AppContext.Dispatcher : null;
        if (dispatcher is null || dispatcher.HasThreadAccess)
        {
            AddEntry(entry);
        }
        else
        {
            dispatcher.Run(() => AddEntry(entry));
        }
    }

    /// <summary>Removes all entries from memory (the log files stay).</summary>
    public void Clear() => Entries.Clear();

    private void AddEntry(LogEntry entry)
    {
        Entries.Add(entry);
        while (Entries.Count > MaxEntries)
        {
            Entries.RemoveAt(0);
        }

        EntryWritten?.Invoke(this, entry);
    }

    private void AppendToFile(LogEntry entry)
    {
        if (!WriteToFile || !HasAppContext)
        {
            return;
        }

        try
        {
            var folder = Path.Combine(AppContext.LocalDataPath, "Logs");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, $"{entry.Time.LocalDateTime:yyyy-MM-dd}.log");
            var line = entry.Details is null ? entry.ToString() : $"{entry}{Environment.NewLine}{entry.Details}";
            lock (_fileLock)
            {
                File.AppendAllText(file, line + Environment.NewLine);
            }
        }
        catch
        {
            // The log must never fail the caller.
        }
    }
}
