using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Infrastructure.Home;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.HomeLanes;

/// <summary>
/// The aw-meta adapter of the home-lanes collector (home spec §3.3, §8) against the reader contract, on aw-meta's own JSON shapes (camelCase keys, PascalCase enum values),
/// and its edges: exit 1 still parses, exit 2, a missing program or JSON this build can't read is unavailable, team-list is optional, and it only ever runs the four reads.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class AwMetaTeamReaderTests : MetaTeamReaderContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Exit_1_with_attention_still_parses()
    {
        var read = await Reader(Script(Team, exitCode: 1)).Read(Token);

        Assert.Null(read.Unavailable);
        Assert.Equal(2, read.Lanes.Count);
    }

    [Theory]
    [InlineData("lanes-context")]
    [InlineData("status")]
    [InlineData("usage")]
    public async Task Exit_2_from_any_read_makes_the_team_unavailable(string command)
    {
        var json = Script(Team);
        var read = await Reader((file, args) => args[0] == command ? new ToolRun(ToolOutcome.Exited, 2, "") : json(file, args)).Read(Token);

        Assert.Contains($"aw-meta {command} exited 2", read.Unavailable, StringComparison.Ordinal);
        Assert.Empty(read.Lanes);
    }

    [Fact]
    public async Task Json_this_build_cannot_read_is_unavailable_not_a_crash()
    {
        var json = Script(Team);
        var read = await Reader((file, args) => args[0] == "status" ? new ToolRun(ToolOutcome.Exited, 0, """{"mergeQueue":[{"pr":"not a number"}]}""") : json(file, args)).Read(Token);

        Assert.StartsWith("aw-meta printed JSON this build can't read", read.Unavailable, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_team_list_that_fails_leaves_the_repository_unknown()
    {
        var json = Script(Team);
        var read = await Reader((file, args) => args[0] == "team-list" ? new ToolRun(ToolOutcome.Exited, 2, "") : json(file, args)).Read(Token);

        Assert.Null(read.Unavailable);
        Assert.Null(read.RepoDirectory);
        Assert.Equal(2, read.MergeQueue.Count);
    }

    [Fact]
    public async Task It_runs_only_the_four_reads_with_json_and_the_program_the_environment_names()
    {
        var runner = new FakeToolRunner(Script(Team));
        var reader = new AwMetaTeamReader(runner, name => name == AwMetaTeamReader.ProgramVariable ? "/opt/aw/aw-meta" : null);

        _ = await reader.Read(Token);

        Assert.Equal(["lanes-context --json", "status --json", "usage --json", "team-list --json"], [.. runner.Calls.Select(static c => string.Join(' ', c.Arguments))]);
        Assert.All(runner.Calls, static c => Assert.Equal("/opt/aw/aw-meta", c.FileName));
    }

    protected override IMetaTeamReader Create(MetaTeamRead team) =>
        team.Unavailable is null ? Reader(Script(team)) : new AwMetaTeamReader(new FakeToolRunner(ToolRun.Missing), static _ => null);

    private static AwMetaTeamReader Reader(Func<string, IReadOnlyList<string>, ToolRun> script) => new(new FakeToolRunner(script), static _ => null);

    // aw-meta's answers for a team, in the shapes the spec checked on 2026-11-12 (and the C# records in src/Meta): every key aw-meta prints, the optional ones left out when null.
    private static Func<string, IReadOnlyList<string>, ToolRun> Script(MetaTeamRead team, int exitCode = 0)
    {
        var lanes = new JsonArray([.. team.Lanes.Select(l => (JsonNode)Drop(new JsonObject
        {
            ["lane"] = l.Lane, ["session"] = l.Session, ["transcript"] = l.Session is null ? null : "/home/owner/.claude/projects/x/" + l.Session + ".jsonl",
            ["tokens"] = l.Tokens, ["lastActivity"] = "2026-11-12T09:00:00+00:00", ["threshold"] = l.Threshold, ["verdict"] = l.Verdict,
            ["reason"] = l.Tokens is null ? "no transcript" : null,
        }))]);
        var context = new JsonObject { ["recycleAt"] = team.RecycleAt, ["lanes"] = lanes, ["attention"] = exitCode == 1 };
        var status = new JsonObject
        {
            ["obligations"] = new JsonArray(),
            ["mergeQueue"] = new JsonArray([.. team.MergeQueue.Select(q => (JsonNode)Drop(new JsonObject { ["pr"] = q.Pr, ["from"] = q.From, ["gate"] = q.Gate, ["github"] = q.Github }))]),
            ["ownerQueue"] = new JsonArray(),
            ["lanes"] = new JsonObject
            {
                ["version"] = 3,
                ["entries"] = new JsonArray([.. team.Lanes.Where(static l => l.SessionName is not null).Select(l => (JsonNode)new JsonObject { ["lane"] = l.Lane, ["sessionName"] = l.SessionName, ["session"] = l.Session, ["source"] = "team" })]),
            },
        };
        var usage = new JsonObject
        {
            ["subjects"] = new JsonArray([.. team.Usage.Select(u => (JsonNode)Drop(new JsonObject
            {
                ["subject"] = u.Subject, ["state"] = u.State, ["kind"] = u.Kind, ["resetsAt"] = u.ResetsAt?.ToString("O", CultureInfo.InvariantCulture), ["agent"] = u.Agent,
                ["signalNote"] = "pane",
            }))]),
            ["attention"] = exitCode == 1,
        };
        var teams = Teams(team);
        return (_, args) => args[0] switch
        {
            "lanes-context" => new ToolRun(ToolOutcome.Exited, exitCode, context.ToJsonString()),
            "status" => new ToolRun(ToolOutcome.Exited, 0, status.ToJsonString()),
            "usage" => new ToolRun(ToolOutcome.Exited, exitCode, usage.ToJsonString()),
            "team-list" => new ToolRun(ToolOutcome.Exited, 0, teams.ToJsonString()),
            _ => new ToolRun(ToolOutcome.Exited, 2, ""),
        };
    }

    private static JsonObject Teams(MetaTeamRead team) => new()
    {
        ["teams"] = new JsonArray(
            new JsonObject { ["name"] = "other", ["layout"] = "warp", ["repo"] = "/home/owner/repos/other", ["lanes"] = new JsonArray(), ["live"] = false, ["selected"] = false },
            new JsonObject { ["name"] = "sindri-v3", ["layout"] = "warp", ["repo"] = team.RepoDirectory, ["lanes"] = new JsonArray("meta"), ["metaSession"] = "sindri-v3-meta", ["live"] = true, ["selected"] = true }),
    };

    // aw-meta leaves a null optional key out (JsonIgnoreCondition.WhenWritingNull)
    private static JsonObject Drop(JsonObject node)
    {
        foreach (var key in node.Where(static p => p.Value is null).Select(static p => p.Key).ToList())
        {
            _ = node.Remove(key);
        }

        return node;
    }
}
