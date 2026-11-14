namespace GreenHive.Wpf.Framework.Mvvm;

/// <summary>Event data for <see cref="AppObject.ErrorOccurred"/> (WIFI: FehlerAufgetretenEventArgs).</summary>
public sealed class ErrorOccurredEventArgs(Exception cause) : EventArgs
{
    /// <summary>The exception that describes the error.</summary>
    public Exception Cause { get; } = cause;
}
