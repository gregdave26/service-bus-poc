using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace ServiceBusPoc.Core.Logging;

/// <summary>
/// Extension methods for configuring structured logging.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Adds console logging with structured format suitable for development and debugging.
    /// Includes ISO 8601 timestamps with timezone offset.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <returns>The logging builder for chaining.</returns>
    public static ILoggingBuilder AddStructuredConsoleLogging(this ILoggingBuilder builder)
    {
        // Do NOT clear providers: Aspire injected OpenTelemetry provider must be preserved
        // to send logs to the Aspire Dashboard. Just enhance the console output with custom formatter.
        
        // Register the custom timestamped formatter for console output
        builder.AddConsole(options =>
        {
            options.FormatterName = TimestampedConsoleFormatter.FormatterName;
        });
        
        // Add the formatter configuration
        builder.AddConsoleFormatter<TimestampedConsoleFormatter, SimpleConsoleFormatterOptions>();

        var otlpEndpoint =
            Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        var logsEndpoint =
            Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_LOGS_ENDPOINT");

        if (!string.IsNullOrWhiteSpace(otlpEndpoint)
            || !string.IsNullOrWhiteSpace(logsEndpoint))
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.SetResourceBuilder(ResourceBuilder.CreateDefault());
                options.AddOtlpExporter();
            });
        }

        return builder;
    }
}
