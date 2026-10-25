using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The 'runs' of an engine-run scoping instance come from its 'launch.started' rows (#1008).</summary>
public sealed class ScopingRunsTests
{
    private static readonly string _address = A.FirstItemId.Value + "/workflow.scoping/1";

    [Fact, Trait("Lane", "unit")]
    public async Task The_draft_and_challenge_launches_become_two_agent_run_records()
    {
        var runs = await ScopingRuns.Of(new Launches([Row("draft", "claude-code"), Row("challenge", "codex")]), _address, TestContext.Current.CancellationToken);

        var parsed = JsonNode.Parse(runs.Value!)!.AsArray();
        Assert.Equal("step.scope.draft", parsed[0]!["step"]!.GetValue<string>());
        Assert.Equal("claude-code", parsed[0]!["target"]!.GetValue<string>());
        Assert.Equal("step.scope.challenge", parsed[1]!["step"]!.GetValue<string>());
        Assert.Equal("codex", parsed[1]!["target"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_challenge_launch_there_are_no_runs_so_the_rule_keeps_warning()
    {
        var runs = await ScopingRuns.Of(new Launches([Row("draft", "claude-code")]), _address, TestContext.Current.CancellationToken);

        Assert.Null(runs.Value);
    }

    private static LaunchRow Row(string state, string agent) =>
        new(LaunchId.From("LCH-" + (state == "draft" ? new string('a', 32) : new string('b', 32))), A.FirstItemId, Repo: null, LaunchMode.Open, AgentId.From(agent), "engine", LaunchState.Started,
            DateTimeOffset.UnixEpoch, OutcomeAt: null, Code: null, Message: null, Stage: null, SessionId: null, ReportedSessionId: null, TabTitle: null, Viewer: null, Late: false, Attach: null,
            CatalogSource: null, ArgvHash: null, Ended: null, new LaunchStep(InstanceAddress.From(_address), state, Attempt: 1), Sequence: 1);

    private sealed class Launches(IReadOnlyList<LaunchRow> rows) : ILaunchViews
    {
        public ValueTask<Result<LaunchRow?>> Get(LaunchId launch, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForItem(ItemId item, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(rows));

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForRepo(RepoName repo, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> Since(DateTimeOffset since, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
