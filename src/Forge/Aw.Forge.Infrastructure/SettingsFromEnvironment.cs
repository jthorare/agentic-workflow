namespace Aw.Forge.Infrastructure;

/// <summary>Reads this package's settings from the environment that Composition passes in.</summary>
internal static class SettingsFromEnvironment
{
    internal static ForgeSettings Read(Func<string, string?> environment)
        => new(StateDirectory: environment("AW_STATE_DIR") is { Length: > 0 } root
            ? Path.Combine(root, "forge")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow", "forge"));
}
