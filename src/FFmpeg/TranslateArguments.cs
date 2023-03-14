internal static partial class FFmpeg
{
    internal static string[] TranslateArguments(string[] args)
    {
        var translated = new List<string>();
        if (TryGetLogLevel(args, out var logLevel))
            translated.Add($"--Logging:LogLevel:Default={logLevel}", "--Logging:LogLevel:Microsoft.Extensions.Hosting=Warning");
        return translated.ToArray();
    }

    private static bool TryGetLogLevel(string[] args, out LogLevel logLevel)
    {
        logLevel = LogLevel.None;
        var i = Array.FindLastIndex(args, option => option is "-v" or "-loglevel");
        if (i < 0 || args.Length <= i + 1)
            return false;
        switch (args[i + 1].Split('+').Last())
        {
            case "quiet" or "-8":
                logLevel = LogLevel.None;
                break;
            case "panic" or "0":
            case "fatal" or "8":
                logLevel = LogLevel.Critical;
                break;
            case "error" or "16":
                logLevel = LogLevel.Error;
                break;
            case "warning" or "24":
                logLevel = LogLevel.Warning;
                break;
            case "info" or "32":
            case "verbose" or "40":
                logLevel = LogLevel.Information;
                break;
            case "debug" or "48":
                logLevel = LogLevel.Debug;
                break;
            case "trace" or "56":
                logLevel = LogLevel.Trace;
                break;
            default:
                return false;
        }
        return true;
    }
}
