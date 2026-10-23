namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Where the T3 host keeps its session map and how it reaches T3 Code.</summary>
/// <param name="MapDirectory">The directory of the session map ('&lt;home&gt;/launch/t3'), one 0600 file per session.</param>
/// <param name="T3Home">T3 Code's data directory ('T3CODE_HOME', default '~/.t3'); its 'userdata/server-runtime.json' names the running server.</param>
/// <param name="Cli">The 't3' program ('T3CODE_CLI_PATH', else 't3' on the PATH).</param>
internal sealed record T3Options(string MapDirectory, string T3Home, string Cli)
{
    /// <summary>The program that opens a 't3code://' link: 'open' on macOS, 'xdg-open' elsewhere.</summary>
    public string Opener { get; init; } = OperatingSystem.IsMacOS() ? "open" : "xdg-open";

    /// <summary>How long one call to T3 (a tool call, or minting a token with the CLI) may take.</summary>
    public TimeSpan CallBudget { get; init; } = TimeSpan.FromSeconds(15);
}
