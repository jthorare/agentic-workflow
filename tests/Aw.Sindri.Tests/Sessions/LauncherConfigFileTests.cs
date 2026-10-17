using System.Text;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The launcher.json reader and the agent catalog (collector spec CA-0): the same verdicts as the schema, built-ins merged with the file's entries.</summary>
public sealed class LauncherConfigFileTests
{
    private static byte[] Text(string json) => Encoding.UTF8.GetBytes(json);

    [Fact, Trait("Lane", "unit")]
    public void Catalog_merges_launcher_json_agents_over_builtins()
    {
        var user = Text("""{ "schema": 1, "agents": { "aider": { "label": "Aider", "bin": "aider", "open": ["{bin}", "--message", "{seed}"], "sessionId": "none", "processName": "aider" }, "claude-code": { "override": true, "bin": "/opt/claude/bin/claude" } } }""");

        var config = LauncherConfigFile.Read(SessionFixtures.Builtins(), user).Value;
        var catalog = new AgentCatalog(config.Agents);

        Assert.Equal(["claude-code", "codex", "cursor", "aider"], catalog.All.Select(static a => a.Id.Value), StringComparer.Ordinal);
        var claude = Assert.IsType<AgentEntry>(catalog.Resolve(AgentId.From("claude-code")));
        Assert.Equal(("Claude Code", "/opt/claude/bin/claude", "claude"), (claude.Label, claude.Bin, claude.ProcessName));
        Assert.Equal("aider", catalog.Resolve(AgentId.From("aider"))?.MatchName);
        Assert.Equal("codex", catalog.Resolve(AgentId.From("codex"))?.MatchName);
        Assert.Null(catalog.Resolve(AgentId.From("nothing")));
    }

    [Fact, Trait("Lane", "unit")]
    public void Builtins_alone_give_the_three_agents_with_their_process_names_and_transcripts()
    {
        var entries = LauncherConfigFile.Read(SessionFixtures.Builtins(), user: null).Value.Agents;

        Assert.Equal([("claude-code", "claude"), ("codex", "codex"), ("cursor", "agent")], entries.Select(static e => (e.Id.Value, e.MatchName)));
        Assert.Equal("~/.claude/projects/*/{sessionId}.jsonl", entries[0].Transcripts);
        Assert.Equal(["{bin}", "--resume", "{sessionId}"], entries[0].Resume);
        Assert.StartsWith("rollout-", entries[1].IdCapture, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_sessions_section_with_an_unknown_key_or_a_poll_out_of_range_is_refused()
    {
        var bogus = LauncherConfigFile.Read(SessionFixtures.Builtins(), Text("""{ "schema": 1, "sessions": { "nope": 1 } }"""));
        var range = LauncherConfigFile.Read(SessionFixtures.Builtins(), Text("""{ "schema": 1, "sessions": { "pollSeconds": 31 } }"""));

        Assert.Equal(["SND-LAUNCH-011", "SND-LAUNCH-011"], [bogus.Error.Code, range.Error.Code]);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_viewer_host_and_meta_sections_are_accepted_and_not_read()
    {
        var config = LauncherConfigFile.Read(SessionFixtures.Builtins(), Text("""
            { "schema": 1, "viewer": { "kind": "print" }, "host": { "kind": "none" }, "meta": { "lanesAgent": "codex", "lanes": { "review": "claude-code" } } }
            """)).Value;

        Assert.Equal(3, config.Agents.Count);
        Assert.Null(config.Sessions);
    }

    [Fact, Trait("Lane", "integration")]
    public void Startup_reads_the_sessions_section_and_lets_the_test_override_replace_the_records_dir()
    {
        var settings = new SindriSettings(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "profile.yaml"));
        var file = Path.Combine(SessionFixtures.Launcher, "sessions.json");
        string? Env(string name) => name switch { "AW_LAUNCHER_CONFIG" => file, _ => null };
        string? Overridden(string name) => name == "AW_SESSIONS_CLAUDE_DIR" ? "/nonexistent-fixture-root/claude-sessions" : Env(name);

        var plain = LauncherStartup.Load(Env, settings);
        var overridden = LauncherStartup.Load(Overridden, settings);

        Assert.Equal((true, 5, false, null), (plain.Sessions.Enabled, plain.Sessions.PollSeconds, plain.Sessions.TestOverride, plain.Sessions.ConfigProblem));
        Assert.Equal(["claude-code", "codex"], plain.Sessions.Providers!.Select(static p => p.Value), StringComparer.Ordinal);
        Assert.Equal("/home/dev/.config/claude/sessions", plain.ClaudeSessionsDirectory);
        Assert.Equal(("/nonexistent-fixture-root/claude-sessions", true), (overridden.ClaudeSessionsDirectory, overridden.Sessions.TestOverride));
    }

    [Fact, Trait("Lane", "integration")]
    public void An_invalid_launcher_json_leaves_the_builtins_and_says_so()
    {
        var settings = new SindriSettings(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "profile.yaml"));
        var file = Path.Combine(SessionFixtures.Launcher, "sessions-bogus.json");

        var load = LauncherStartup.Load(name => name == "AW_LAUNCHER_CONFIG" ? file : null, settings);

        Assert.Equal(3, load.Catalog.All.Count);
        Assert.NotNull(load.Sessions.ConfigProblem);
        Assert.True(load.Sessions.Enabled);
    }

    private static LauncherLoad LoadText(string json)
    {
        var directory = Directory.CreateTempSubdirectory("aw-launcher").FullName;
        try
        {
            var file = Path.Combine(directory, "launcher.json");
            File.WriteAllText(file, json);
            return LauncherStartup.Load(name => name == "AW_LAUNCHER_CONFIG" ? file : null, new SindriSettings(directory, Path.Combine(directory, "profile.yaml")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public void A_typo_under_meta_leaves_the_collector_off_and_raises_no_problem()
    {
        var load = LoadText("""{ "schema": 1, "sessions": { "enabled": false }, "meta": { "lanesAgent": "claude-code", "typo": 1 } }""");

        Assert.Equal((false, null), (load.Sessions.Enabled, load.Sessions.ConfigProblem));
        Assert.Empty(load.AttendedProblems);
    }

    [Fact, Trait("Lane", "integration")]
    public void A_bad_viewer_or_host_is_recorded_for_attended_opens_and_not_for_the_collector()
    {
        var load = LoadText("""{ "schema": 1, "sessions": { "pollSeconds": 7 }, "viewer": { "kind": "nope" }, "host": { "kind": "screen" } }""");

        Assert.Equal((7, null), (load.Sessions.PollSeconds, load.Sessions.ConfigProblem));
        Assert.Equal(2, load.AttendedProblems.Count);
    }

    [Fact, Trait("Lane", "integration")]
    public void A_bad_agent_entry_drops_only_itself()
    {
        var load = LoadText("""{ "schema": 1, "agents": { "broken": { "label": "x" }, "aider": { "label": "Aider", "bin": "aider", "open": ["{bin}"], "sessionId": "none", "processName": "aider" } } }""");

        Assert.Equal(["claude-code", "codex", "cursor", "aider"], load.Catalog.All.Select(static a => a.Id.Value), StringComparer.Ordinal);
        Assert.NotNull(load.Sessions.ConfigProblem);
    }

    [Fact, Trait("Lane", "integration")]
    public void A_bad_sessions_section_falls_back_to_the_defaults_but_never_turns_a_disabled_collector_on()
    {
        var off = LoadText("""{ "schema": 1, "sessions": { "enabled": false, "pollSeconds": 99 } }""");
        var on = LoadText("""{ "schema": 1, "sessions": { "pollSeconds": 99 } }""");

        Assert.Equal((false, true), (off.Sessions.Enabled, off.Sessions.ConfigProblem is not null));
        Assert.Equal((true, 2, true), (on.Sessions.Enabled, on.Sessions.PollSeconds, on.Sessions.ConfigProblem is not null));
    }
}
