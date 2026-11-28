namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The 'default-terminal' viewer's settings: it takes nothing from launcher.json, so every member is a seam for tests.</summary>
internal sealed record DefaultTerminalOptions
{
    /// <summary>Variables added to the opener process (tests point PATH at fake openers); TMUX is always removed.</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>Reads an environment variable of this process (the GUI-session check); the real environment when null.</summary>
    public Func<string, string?>? GetEnv { get; init; }

    /// <summary>True on macOS (a '.command' file through 'open'); this OS when not set (tests pin it).</summary>
    public bool IsMac { get; init; } = OperatingSystem.IsMacOS();

    /// <summary>Answers whether a program resolves on the login path; <c lang="text">ViewerProcess.OnLoginPath</c> (a real login shell) when null.</summary>
    public Func<string, CancellationToken, ValueTask<bool>>? OnPath { get; init; }

    /// <summary>The folder the private '.command' directory goes under (macOS); the system temp folder when null.</summary>
    public string? TempRoot { get; init; }
}
