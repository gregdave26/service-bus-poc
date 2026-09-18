using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using System.Globalization;

namespace ServiceBusPoc.Core.Logging;

/// <summary>
/// A console formatter that includes ISO 8601 timestamps with timezone offset.
/// Formats logs as: "2026-09-17T10:41:29.527+08:00 info: Category[EventId] Message"
/// </summary>
public sealed class TimestampedConsoleFormatter : ConsoleFormatter
{
    /// <summary>
    /// The formatter name used to register with DI.
    /// </summary>
    public const string FormatterName = "timestamped";

    public TimestampedConsoleFormatter() : base(FormatterName)
    {
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        ArgumentNullException.ThrowIfNull(textWriter);

        // Get the timestamp with timezone offset (ISO 8601 format)
        var timestamp = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);

        // Get the log level name
        var logLevelName = GetLogLevelName(logEntry.LogLevel);

        // Write timestamp
        textWriter.Write(timestamp);
        textWriter.Write(" ");

        // Write log level
        textWriter.Write(logLevelName);
        textWriter.Write(": ");

        // Write category and event ID
        textWriter.Write(logEntry.Category);
        textWriter.Write("[");
        textWriter.Write(logEntry.EventId.Id);
        textWriter.Write("]");
        textWriter.Write("\n      ");

        // Write message
        textWriter.Write(logEntry.Formatter(logEntry.State, logEntry.Exception));

        // Write exception if present
        if (logEntry.Exception != null)
        {
            textWriter.Write("\n");
            textWriter.Write(logEntry.Exception);
        }

        textWriter.Write("\n");
    }

    private static string GetLogLevelName(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => "trce",
        LogLevel.Debug => "dbug",
        LogLevel.Information => "info",
        LogLevel.Warning => "warn",
        LogLevel.Error => "fail",
        LogLevel.Critical => "crit",
        _ => "unkn"
    };
}
