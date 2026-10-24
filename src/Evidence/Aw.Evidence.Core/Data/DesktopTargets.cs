using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>
/// The 'desktop' part of the targets config: how desktop-run seeds the app's data, starts the app with its UI driver on, which driver command
/// scripts it, and where the repo's screens manifest is. Nothing in it names an app: the commands are the repo's own.
/// </summary>
/// <param name="Manifest">The screens manifest (docs: ui-evidence.desktop-screens/1), relative to the repo.</param>
/// <param name="HomeEnv">The variable that carries the scratch directory the fixture and the app keep their state in.</param>
/// <param name="Fixture">The command that builds and tears down the seeded data.</param>
/// <param name="App">The command that starts the app; desktop-run adds AW_UI_DRIVER=1 and AW_UI_DRIVER_DIR.</param>
/// <param name="Driver">The UI driver's command line (aw-ui-driver); null runs 'aw-ui-driver' from PATH.</param>
/// <param name="Sizes">The window sizes to capture, 'WxH'; default 1440x900 and 900x900.</param>
/// <param name="Threshold">The default limit of differing pixels, in percent; default 10.</param>
/// <param name="TimeoutSeconds">How long each command may run; default 300.</param>
/// <param name="Env">Extra environment for every command (literal values, never secrets).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesktopTargets(
    string Manifest,
    string HomeEnv,
    DesktopFixture Fixture,
    DesktopApp App,
    DesktopDriver? Driver = null,
    IReadOnlyList<string>? Sizes = null,
    double? Threshold = null,
    int? TimeoutSeconds = null,
    IReadOnlyDictionary<string, string>? Env = null)
{
    /// <summary>The limit of differing pixels when neither the config nor a screen sets one, in percent.</summary>
    public const double DefaultThreshold = 10;

    /// <summary>How long each command may run when the config sets no limit.</summary>
    public const int DefaultTimeoutSeconds = 300;

    /// <summary>The driver program when the config names none.</summary>
    public const string DefaultDriver = "aw-ui-driver";

    /// <summary>The window sizes captured when the config names none.</summary>
    public static IReadOnlyList<string> DefaultSizes { get; } = ["1440x900", "900x900"];
}
