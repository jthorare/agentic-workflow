namespace Aw.CodeIndex.Infrastructure.Graph;

/// <summary>How graphify is found and bounded. The pin is context/code-index.lock's graphify row (the version and the first 12 hex digits of the requirements hash name the environment setup.sh installs).</summary>
/// <param name="Pin">The version this build accepts.</param>
/// <param name="Binary">The absolute path of the graphify in the dedicated environment; never looked up on PATH.</param>
/// <param name="TempRoot">The parent of each run's private directory.</param>
internal sealed record GraphifyOptions(string Pin, string Binary, string TempRoot)
{
    private const string LockedVersion = "0.9.67";
    private const string LockedRequirements = "ebe4304d077e";

    /// <summary>The longest one run may take.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>The largest single file copied into the snapshot.</summary>
    public long MaxFileBytes { get; init; } = 4L * 1024 * 1024;

    /// <summary>The most bytes copied into the snapshot.</summary>
    public long MaxSnapshotBytes { get; init; } = 512L * 1024 * 1024;

    /// <summary>The machine; tests swap it.</summary>
    public GraphHost Host { get; init; } = GraphHost.Current();

    /// <summary>Wraps a command in the sandbox (null: this machine has none); tests swap it for a pass-through.</summary>
    public Func<GraphHost, IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>?> Sandbox { get; init; } = GraphSandbox.Argv;

    /// <summary>The shipped options from the environment: AW_STATE_DIR locates the environment setup.sh installed; AW_CODEINDEX_GRAPHIFY overrides the binary.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The options.</returns>
    public static GraphifyOptions FromEnvironment(Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var state = environment("AW_STATE_DIR") is { Length: > 0 } root ? root : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow");
        var binary = environment("AW_CODEINDEX_GRAPHIFY") is { Length: > 0 } given
            ? given
            : Path.Combine(state, "cache", "code-index", $"graphify-{LockedVersion}-{LockedRequirements}", "bin", "graphify");
        return new GraphifyOptions(LockedVersion, binary, Path.Combine(state, "tmp", "code-index"));
    }
}
