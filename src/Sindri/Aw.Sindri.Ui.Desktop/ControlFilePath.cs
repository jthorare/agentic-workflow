namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// Where the control plane writes control.json and its per-start human token on this machine (docs/csharp/sindri.md
/// "Files"): AW_SINDRI_HOME, else $AW_STATE_DIR/sindri-app, else ~/.agentic-workflow/sindri-app.
/// </summary>
internal static class ControlFilePath
{
    internal static string From(Func<string, string?> environment, string userHome) => Path.Combine(Home(environment, userHome), "control.json");

    /// <summary>The human token serve mints at each start (API spec §6): auth/human.token under the same home.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="userHome">The user's home directory.</param>
    /// <returns>The token file's path.</returns>
    internal static string TokenFrom(Func<string, string?> environment, string userHome) => Path.Combine(Home(environment, userHome), "auth", "human.token");

    private static string Home(Func<string, string?> environment, string userHome) =>
        environment("AW_SINDRI_HOME") is { Length: > 0 } explicitHome
            ? explicitHome
            : Path.Combine(environment("AW_STATE_DIR") is { Length: > 0 } root ? root : Path.Combine(userHome, ".agentic-workflow"), "sindri-app");
}
