using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Core.Data;

/// <summary>The desktop config after its checks: sizes parsed, defaults filled in.</summary>
/// <param name="Manifest">The screens manifest, relative to the repo.</param>
/// <param name="HomeEnv">The variable that carries the scratch directory.</param>
/// <param name="Fixture">The seeded-data command.</param>
/// <param name="App">The command that starts the app.</param>
/// <param name="DriverCommand">The UI driver's program and leading arguments.</param>
/// <param name="Sizes">The window sizes to capture.</param>
/// <param name="Threshold">The default limit of differing pixels, in percent.</param>
/// <param name="Timeout">How long each command may run.</param>
/// <param name="Env">Extra environment for every command.</param>
public sealed record ResolvedDesktop(
    string Manifest,
    string HomeEnv,
    DesktopFixture Fixture,
    DesktopApp App,
    IReadOnlyList<string> DriverCommand,
    IReadOnlyList<ViewportSize> Sizes,
    double Threshold,
    TimeSpan Timeout,
    IReadOnlyDictionary<string, string> Env);
