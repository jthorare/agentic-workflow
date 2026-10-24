namespace Aw.Evidence.Features.DesktopRun;

/// <summary>The last lines of a command's standard error, on one line, for an error message.</summary>
internal static class CommandTail
{
    private const int TailLines = 3;
    private const int TailLimit = 300;

    internal static string Of(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tail = string.Join(" | ", lines.TakeLast(TailLines));
        return tail.Length <= TailLimit ? tail : "…" + tail[^TailLimit..];
    }
}
