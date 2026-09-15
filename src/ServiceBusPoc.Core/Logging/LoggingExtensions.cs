using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace ServiceBusPoc.Core.Logging;

/// <summary>
/// Extension methods for configuring structured logging.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Adds console logging with structured format suitable for development and debugging.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <returns>The logging builder for chaining.</returns>
    public static ILoggingBuilder AddStructuredConsoleLogging(this ILoggingBuilder builder)
    {
        builder.ClearProviders();
        builder.AddConsole(options =>
        {
            options.FormatterName = "simple";
        });

        return builder;
    }
}
