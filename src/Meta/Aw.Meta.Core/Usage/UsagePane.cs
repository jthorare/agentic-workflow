namespace Aw.Meta.Core.Usage;

/// <summary>Reads a lane's screen for a usage-limit stop.</summary>
internal static class UsagePane
{
    private const int Window = 80;
    private const int BusyWindow = 15;
    private const string Placeholder = "Try \"";

    /// <summary>Reads the end of a pane capture.</summary>
    /// <param name="capture">What tmux capture-pane printed.</param>
    /// <param name="reference">When the screen was looked at.</param>
    /// <param name="local">The zone of a reset time that names none.</param>
    /// <returns>What the screen says.</returns>
    internal static PaneUsage Read(string capture, DateTimeOffset reference, TimeZoneInfo local)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var all = capture.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
        var lines = all.Length > Window ? all[^Window..] : all;
        var at = Array.FindLastIndex(lines, UsageLimitParser.Mentions);
        if (at < 0)
        {
            return new PaneUsage(Stop: null, Current: false, Idle: false);
        }

        var stop = UsageLimitParser.Parse(lines[at], reference, local, seenNow: true);
        if (stop is { ResetsAt: null } && at + 1 < lines.Length && Continues(lines[at + 1]))
        {
            stop = UsageLimitParser.Parse(lines[at].TrimEnd() + " " + lines[at + 1].Trim(), reference, local, seenNow: true) ?? stop;
        }

        var current = !lines.Skip(at + 1).Any(Output);
        var busy = lines.TakeLast(BusyWindow).Any(static l => l.Contains("to interrupt", StringComparison.Ordinal));
        var prompt = Coordination.PromptRow.Find(lines);
        var empty = prompt > at && Empty(lines[prompt]);
        return new PaneUsage(stop, current, current && !busy && empty);
    }

    // The wrapped second half of a message line: text, not a rule, a prompt or output.
    private static bool Continues(string line)
    {
        var text = line.Trim();
        return text.Length > 0 && !text.StartsWith('─') && !text.StartsWith('❯') && !Output(line) && !UsageLimitParser.Mentions(line);
    }

    // What the lane printed or was told after the stop: an assistant block or an echoed user prompt.
    private static bool Output(string line)
    {
        var text = line.TrimStart();
        return text.StartsWith('⏺') || text.StartsWith("> ", StringComparison.Ordinal);
    }

    private static bool Empty(string promptLine)
    {
        var typed = promptLine[1..].Trim();
        return typed.Length == 0 || typed.StartsWith(Placeholder, StringComparison.Ordinal);
    }
}
