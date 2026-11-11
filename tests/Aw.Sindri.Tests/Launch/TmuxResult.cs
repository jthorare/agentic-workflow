namespace Aw.Sindri.Tests.Launch;

/// <summary>One tmux call: its stdout when it exited 0, else null, and what to print when it did not.</summary>
internal sealed record TmuxResult(string? Output, int? ExitCode, string Stderr, bool TimedOut)
{
    public bool Ok => Output is not null;

    public string Describe() => TimedOut ? Stderr : $"tmux exit {ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"}: {Stderr}";
}
