namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The 'warp' viewer's settings.</summary>
/// <param name="LaunchDir">Warp's launch-configuration folder (or AW_WARP_LAUNCH_DIR).</param>
internal sealed record WarpViewerOptions(string LaunchDir)
{
    /// <summary>The program that opens a URI: 'open' on macOS, 'xdg-open' elsewhere.</summary>
    public string Opener { get; init; } = "xdg-open";

    /// <summary>True on macOS, where the handler check and the GUI check differ.</summary>
    public bool IsMac { get; init; }

    /// <summary>Reads an environment variable of this process (the GUI-session check); the real environment when null.</summary>
    public Func<string, string?>? GetEnv { get; init; }
}
