using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Reads the built-in catalog and the box's launcher.json once at start (collector spec CO13, §9).</summary>
internal static class LauncherStartup
{
    /// <summary>The catalog and sessions settings; an invalid launcher.json leaves the built-ins and the defaults, with the reason as the config problem.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="settings">Sindri's settings (the state directory locates launcher.json).</param>
    /// <returns>The loaded catalog, settings and the sessions directory.</returns>
    internal static LauncherLoad Load(Func<string, string?> environment, SindriSettings settings)
    {
        var builtins = Builtins();
        var path = ConfigPath(environment, settings);
        var (bytes, unreadable) = ReadFile(path);
        var config = IsolatedLauncherRead.Read(builtins, bytes);
        var problem = unreadable ?? config.Problem;
        var section = config.Sessions;
        return new LauncherLoad(new AgentCatalog(config.Agents), Sessions(section, problem, TestOverride(environment)), ClaudeDirectory(environment, section?.ClaudeSessionsDir)) { AttendedProblems = config.AttendedProblems };
    }

    /// <summary>The catalog that re-reads launcher.json when it changes (the launcher's, one read per open).</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="settings">Sindri's settings.</param>
    /// <returns>The reloading catalog.</returns>
    internal static ReloadingAgentCatalog Catalog(Func<string, string?> environment, SindriSettings settings) =>
        new(ConfigPath(environment, settings), Builtins());

    /// <summary>The file in force: AW_LAUNCHER_CONFIG, else launcher.json in the state directory (precedence: environment, file, built-ins).</summary>
    internal static string ConfigPath(Func<string, string?> environment, SindriSettings settings) =>
        NonEmpty(environment("AW_LAUNCHER_CONFIG")) ?? Path.Combine(settings.StateDirectory, "launcher.json");

    private static bool TestOverride(Func<string, string?> environment) =>
        NonEmpty(environment("AW_SESSIONS_PROC_ROOT")) is not null || NonEmpty(environment("AW_SESSIONS_CLAUDE_DIR")) is not null;

    private static SessionSettings Sessions(SessionsSection? section, string? problem, bool overridden) =>
        new(section?.Enabled ?? true, section?.PollSeconds ?? 2, Providers(section), overridden) { ConfigProblem = problem };

    private static IReadOnlyList<AgentId>? Providers(SessionsSection? section) =>
        section?.Providers is { } named ? [.. named.Select(AgentId.From)] : null;

    internal static string? NonEmpty(string? value) => value is { Length: > 0 } ? value : null;

    private static string ClaudeDirectory(Func<string, string?> environment, string? configured)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var chosen = NonEmpty(environment("AW_SESSIONS_CLAUDE_DIR")) ?? configured ?? "~/.claude/sessions";
        return chosen.StartsWith('~') ? home + chosen[1..] : chosen;
    }

    internal static byte[] Builtins()
    {
        using var stream = typeof(LauncherStartup).Assembly.GetManifestResourceStream("builtins.json")
            ?? throw new InvalidOperationException("builtins.json isn't embedded in Aw.Sindri.Infrastructure");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    internal static (byte[]? Bytes, string? Problem) ReadFile(string path)
    {
        try
        {
            return File.Exists(path) ? (File.ReadAllBytes(path), null) : (null, null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (null, SindriErrors.LauncherInvalid.With("launcher.json can't be read: " + e.GetType().Name).Message);
        }
    }
}
