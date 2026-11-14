namespace GreenHive.Wpf.Framework.Mvvm;

/// <summary>Type of a log entry.</summary>
public enum LogEntryType
{
    Information,
    Warning,
    Error,
    NewInstance,
    Start,
    End,
}

/// <summary>One entry of the application log.</summary>
public sealed record LogEntry(DateTimeOffset Time, LogEntryType Type, string Text, string? Source, string? Details)
{
    public string TimeText => Time.ToLocalTime().ToString("HH:mm:ss.fff");

    public string TypeText => Type.ToString();

    public override string ToString() => $"{TimeText} [{Type}] {Text}";
}
