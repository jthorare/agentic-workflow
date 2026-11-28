namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The 'default-terminal' viewer's settings: it takes nothing from launcher.json, so every member is a seam for tests.</summary>
internal sealed record DefaultTerminalOptions
{
    /// <summary>Variables added to the opener process; TMUX is always removed.</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>Reads an environment variable of this process (the GUI-session check and AW_META_COMMAND_GRACE); the real environment when null.</summary>
    public Func<string, string?>? GetEnv { get; init; }

    /// <summary>True on macOS (a '.command' file through 'open'); this OS when not set (tests pin it).</summary>
    public bool IsMac { get; init; } = OperatingSystem.IsMacOS();

    /// <summary>Resolves a program to the path the login shell finds, or null when it is missing; <c lang="text">ViewerProcess.OnLoginPath</c>-style real lookup when null.</summary>
    public Func<string, CancellationToken, ValueTask<string?>>? Resolve { get; init; }

    /// <summary>Runs every resolved program as '&lt;this&gt; &lt;path&gt; args' (tests set 'sh' so a fresh stub script is never exec'd, ETXTBSY); null runs the program itself.</summary>
    public string? RunWith { get; init; }

    /// <summary>The folder the private '.command' directory goes under (macOS); the system temp folder when null.</summary>
    public string? TempRoot { get; init; }
}
