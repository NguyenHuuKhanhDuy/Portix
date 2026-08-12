namespace Portix.Shared.Logging;

/// <summary>
/// Writes one colored, single-line entry per request directly to the console, bypassing
/// ILogger so the status code can drive per-token color instead of a single per-level color.
/// Uses only Console.ForegroundColor/ResetColor, which are no-ops when output is redirected
/// (e.g. piped to a file), so redirected output degrades to plain, uncolored text.
/// </summary>
public static class RequestConsoleLog
{
    /// <summary>Logs a request that received a real HTTP response, colored by status class.</summary>
    public static void Write(string method, string path, int statusCode, double elapsedMs)
    {
        var color = statusCode switch
        {
            >= 200 and < 300 => ConsoleColor.Green,
            >= 300 and < 400 => ConsoleColor.Cyan,
            >= 400 and < 500 => ConsoleColor.Yellow,
            _ => ConsoleColor.Red,
        };

        WriteLine(color, $"{method} {path} {statusCode} {elapsedMs:0}ms");
    }

    /// <summary>Logs a request that never reached its destination, colored distinctly from any real status class.</summary>
    public static void WriteGatewayError(string method, string path, string reason)
    {
        WriteLine(ConsoleColor.Magenta, $"{method} {path} - {reason}");
    }

    private static void WriteLine(ConsoleColor color, string line)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(line);
        Console.ForegroundColor = previous;
    }
}
