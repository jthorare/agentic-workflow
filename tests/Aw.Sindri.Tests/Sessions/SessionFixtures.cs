namespace Aw.Sindri.Tests.Sessions;

/// <summary>Where the shared contract fixtures are: tests/fixtures/sessions (a /proc-shaped tree and Claude's records), tests/fixtures/fleet and tests/fixtures/launcher.</summary>
internal static class SessionFixtures
{
    /// <summary>The uid the fixture tree treats as its own (manifest.json 'ownUid').</summary>
    public const int OwnUid = 1000;

    public static string Root { get; } = FindRoot();

    public static string Proc => Path.Combine(Root, "tests", "fixtures", "sessions", "proc");

    public static string Macos => Path.Combine(Root, "tests", "fixtures", "sessions", "macos");

    public static string ClaudeSessions => Path.Combine(Root, "tests", "fixtures", "sessions", "claude-sessions");

    public static string Fleet => Path.Combine(Root, "tests", "fixtures", "fleet");

    public static string Launcher => Path.Combine(Root, "tests", "fixtures", "launcher");

    public static byte[] Builtins() => File.ReadAllBytes(Path.Combine(Root, "config", "launcher", "builtins.json"));

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("No AgenticWorkflow.slnx above the test's base directory.");
    }
}
