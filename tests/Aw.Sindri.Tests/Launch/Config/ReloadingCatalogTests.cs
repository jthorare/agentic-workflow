using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Tests.Launch.Config;

/// <summary>The catalog a launch reads: env, then the file, then the built-ins, re-read when the file changes (one read per open, not per start).</summary>
public sealed class ReloadingCatalogTests : IDisposable
{
    private const string AiderOnly = """{ "schema": 1, "agents": { "aider": { "label": "Aider", "bin": "aider", "open": ["{bin}"], "sessionId": "none" } } }""";

    private readonly string _home = Directory.CreateTempSubdirectory("aw-catalog").FullName;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private string Write(string name, string text, DateTime? stamp = null)
    {
        var path = Path.Combine(_home, name);
        File.WriteAllText(path, text);
        File.SetLastWriteTimeUtc(path, stamp ?? new DateTime(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc));
        return path;
    }

    private ReloadingAgentCatalog Catalog(string? env = null) =>
        LauncherStartup.Catalog(name => name == "AW_LAUNCHER_CONFIG" ? env : null, new SindriSettings(_home, Path.Combine(_home, "profile.yaml")) { StateDirectory = _home });

    private static void AssertIds(ReloadingAgentCatalog catalog, params string[] expected) => Assert.Equal(expected, [.. catalog.All.Select(static a => a.Id.Value)], StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void With_no_file_and_no_env_the_catalog_is_the_builtins() =>
        AssertIds(Catalog(), "claude-code", "codex", "cursor");

    [Fact, Trait("Lane", "unit")]
    public void The_state_directory_file_is_read_when_the_env_names_none()
    {
        _ = Write("launcher.json", AiderOnly);

        AssertIds(Catalog(), "claude-code", "codex", "cursor", "aider");
    }

    [Fact, Trait("Lane", "unit")]
    public void The_env_file_wins_over_the_state_directory_file()
    {
        _ = Write("launcher.json", AiderOnly);
        var env = Write("other.json", AiderOnly.Replace("aider", "tool", StringComparison.Ordinal));

        var catalog = Catalog(env);

        AssertIds(catalog, "claude-code", "codex", "cursor", "tool");
        Assert.Equal(env, catalog.Path);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_env_naming_a_missing_file_gives_the_builtins_not_the_state_directory_file()
    {
        _ = Write("launcher.json", AiderOnly);

        AssertIds(Catalog(Path.Combine(_home, "nowhere.json")), "claude-code", "codex", "cursor");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_changed_file_is_read_again_and_an_unchanged_one_is_not()
    {
        var stamp = new DateTime(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc);
        var path = Write("launcher.json", AiderOnly, stamp);
        var catalog = Catalog();
        var first = catalog.Current;

        Assert.Same(first, catalog.Current);

        _ = Write("launcher.json", AiderOnly.Replace("aider", "tool", StringComparison.Ordinal), stamp.AddSeconds(1));

        AssertIds(catalog, "claude-code", "codex", "cursor", "tool");
        Assert.NotSame(first, catalog.Current);
        Assert.Equal(path, catalog.Path);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_rewritten_to_the_same_size_within_one_timestamp_tick_is_still_seen_by_its_new_time()
    {
        var stamp = new DateTime(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc);
        _ = Write("launcher.json", AiderOnly, stamp);
        var catalog = Catalog();
        _ = catalog.Current;

        _ = Write("launcher.json", AiderOnly.Replace("aider", "bider", StringComparison.Ordinal), stamp.AddTicks(1));

        Assert.Contains(catalog.All, static a => a.Id.Value == "bider");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_deleted_file_gives_the_builtins_again_and_a_broken_one_keeps_them_with_the_problem()
    {
        var path = Write("launcher.json", AiderOnly);
        var catalog = Catalog();
        Assert.Equal(4, catalog.All.Count);

        File.Delete(path);
        Assert.Equal(3, catalog.All.Count);

        _ = Write("launcher.json", "not json");
        Assert.Equal(3, catalog.All.Count);
        Assert.True(catalog.Current.Unusable);
    }

    [Fact, Trait("Lane", "unit")]
    public void Resolve_answers_from_the_current_read()
    {
        var stamp = new DateTime(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc);
        _ = Write("launcher.json", AiderOnly, stamp);
        var catalog = Catalog();
        Assert.Null(catalog.Resolve(AgentId.From("tool")));

        _ = Write("launcher.json", AiderOnly.Replace("aider", "tool", StringComparison.Ordinal), stamp.AddSeconds(2));

        Assert.NotNull(catalog.Resolve(AgentId.From("tool")));
    }
}
