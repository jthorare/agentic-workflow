namespace Aw.Evidence.Features.DesktopRun;

/// <summary>Runs the repo's desktop app on seeded data, drives each screen through the app's UI driver, renders their mockups at the same size and compares them.</summary>
/// <param name="ManifestPath">The screens manifest (absolute).</param>
/// <param name="RunDirectory">The run directory (absolute); its name is the run id.</param>
/// <param name="Repo">The repository under test (absolute): where the fixture, app and driver commands run and the evidence config is looked for.</param>
/// <param name="LockTimeout">How long to wait for each machine lock.</param>
/// <param name="AppBuild">The app build (commit) under test, when verified.</param>
/// <param name="ConfigPath">An evidence config (absolute) used instead of the repo's, when given.</param>
/// <param name="Screens">Only these screens (names from the manifest); every screen when null.</param>
public sealed record DesktopRunRequest(
    string ManifestPath,
    string RunDirectory,
    string Repo,
    TimeSpan LockTimeout,
    string? AppBuild = null,
    string? ConfigPath = null,
    IReadOnlyList<string>? Screens = null);
