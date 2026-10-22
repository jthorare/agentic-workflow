using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>An agent Step in a CHILD instance (engine P8 proof F1): the child starts with its declared inputs only, so it has no 'fact:item'; the launch finds the item's repo from the instance's item instead.</summary>
[Lane(TestLane.Integration)]
[Collection(nameof(WallClockAlone))]
public sealed class AgentChildLaunchTests : IDisposable
{
    private const string Repo = "/work/app";
    private const string PhaseWorktree = "/work/app/.claude/worktrees/sindri-line-p1";
    private const string ItemKeyText = "ITM-00000000000000000000000000000007";
    private readonly WorkflowsHarness _harness = new();
    private readonly FakeLauncher _launcher = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task A_childs_agent_step_launches_in_the_items_repo()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Replacing = Register;
        _ = _harness.Approve(_child);
        _ = _harness.Approve(_parent);
        await using var server = new SindriTimeSkippingServer();
        await server.InitializeAsync();
        await using var sindri = _harness.Sindri(_harness.Replacing);
        await WorkflowsHarness.HoldLease(sindri, ct);
        await ApproveTarget(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(async () =>
        {
            var start = await WorkflowsHarness.RecipeStart(sindri, "workflow.t-agent-parent", ItemKeyText, ct);
            var item = _harness.Fact(JsonSerializer.Serialize(new { id = ItemKeyText, repo = "app", spec = (string?)null }));
            var inputs = new Dictionary<FactKey, InputFact>
            {
                [FactKey.From("fact:item")] = new InputFact("item.json", item),
                [FactKey.From("fact:request")] = new InputFact("request.txt", _harness.Fact("Fix it.")),
            };
            var parent = await WorkflowsHarness.Start(server.Environment.Client, start with { Inputs = inputs });
            await WaitFor(() => _launcher.Requests.Count > 0, ct);

            var request = Assert.Single(_launcher.Requests);
            Assert.Equal((ItemKeyText, Repo), (request.Item.Value, request.RepoPath));
            await parent.TerminateAsync("test over");
        }, ct);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_childs_agent_step_launches_in_the_phase_worktree_it_is_handed()
    {
        var request = await LaunchedWith(workspace: PhaseWorktree, ct: TestContext.Current.CancellationToken);

        Assert.Equal(PhaseWorktree, request.Worktree);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_childs_agent_step_without_a_phase_worktree_gets_the_per_run_worktree_as_before()
    {
        var request = await LaunchedWith(workspace: null, ct: TestContext.Current.CancellationToken);

        Assert.StartsWith(Repo + "/.claude/worktrees/sindri-", request.Worktree, StringComparison.Ordinal);
    }

    private async Task<LaunchRequest> LaunchedWith(string? workspace, CancellationToken ct)
    {
        _harness.Replacing = Register;
        _ = _harness.Approve(workspace is null ? _child : _workspaceChild);
        _ = _harness.Approve(workspace is null ? _parent : _workspaceParent);
        await using var server = new SindriTimeSkippingServer();
        await server.InitializeAsync();
        await using var sindri = _harness.Sindri(_harness.Replacing);
        await WorkflowsHarness.HoldLease(sindri, ct);
        await ApproveTarget(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        LaunchRequest? seen = null;
        await worker.ExecuteAsync(async () =>
        {
            var start = await WorkflowsHarness.RecipeStart(sindri, workspace is null ? "workflow.t-agent-parent" : "workflow.t-ws-parent", ItemKeyText, ct);
            var item = _harness.Fact(JsonSerializer.Serialize(new { id = ItemKeyText, repo = "app", spec = (string?)null }));
            var inputs = new Dictionary<FactKey, InputFact>
            {
                [FactKey.From("fact:item")] = new InputFact("item.json", item),
                [FactKey.From("fact:request")] = new InputFact("request.txt", _harness.Fact("Fix it.")),
            };
            if (workspace is not null)
            {
                inputs[FactKey.From("fact:workspace")] = new InputFact("workspace.txt", _harness.Fact(workspace));
            }

            var parent = await WorkflowsHarness.Start(server.Environment.Client, start with { Inputs = inputs });
            await WaitFor(() => _launcher.Requests.Count > 0, ct);
            seen = Assert.Single(_launcher.Requests);
            await parent.TerminateAsync("test over");
        }, ct);
        return seen!;
    }

    private static async Task WaitFor(Func<bool> condition, CancellationToken ct)
    {
        var deadline = RealTime.NowDateTime.AddSeconds(30);
        while (!condition() && RealTime.NowDateTime < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.True(condition());
    }

    private static async Task ApproveTarget(IServiceProvider sindri, CancellationToken ct)
    {
        var declared = new SindriProfile(SindriProfile.CurrentSchemaVersion, "tester", Repos: [new RepoEntry("app", Repo)],
            Workflow: new WorkflowSettings(Agent: new WorkflowAgentSettings(AgentId.From("claude-code"))));
        var profile = ProfileRules.Check(declared).Value;
        var saved = await sindri.GetRequiredService<IApprovals>().Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), ct);
        Assert.False(saved.IsError, saved.IsError ? saved.Error.Message : null);
    }

    private void Register(IServiceCollection services)
    {
        _ = services.Replace(ServiceDescriptor.Singleton<ILauncher>(_launcher));
        _ = services.Replace(ServiceDescriptor.Singleton<IAgentSessions>(new FakeAgentSessions()));
        _ = services.Replace(ServiceDescriptor.Singleton<IRunDiff>(new FakeRunDiff()));
        _ = services.Replace(ServiceDescriptor.Singleton<IItemRepos>(new AppRepos()));
    }

    private sealed class AppRepos : IItemRepos
    {
        public ValueTask<Result<RepoName?>> RepoOf(ItemId item, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success<RepoName?>(item.Value == ItemKeyText ? RepoName.From("app") : null));

        public ValueTask<Result<bool>> Exists(ItemId item, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(item.Value == ItemKeyText));
    }

    private static readonly string _child = string.Join('\n',
        "schema: 1",
        "id: workflow.t-agent-child",
        "version: 1",
        "inputs:",
        "  - { key: fact:request, label: \"Request\", type: text, kind: text }",
        "initial: write",
        "states:",
        "  write:",
        "    kind: active",
        "    step: step.report.write",
        "    budget: { tokens: 1000 }",
        "    timeout: { after: 1m, then: park }",
        "    next: \"Write up the resolution for {item}.\"",
        "  done: { kind: terminal, outcome: resolved }",
        "transitions:",
        "  - { from: write, to: done, on: step.completed, when: { outcome: done }, produces: [fact:resolution] }");

    private static readonly string _parent = string.Join('\n',
        "schema: 1",
        "id: workflow.t-agent-parent",
        "version: 1",
        "inputs:",
        "  - { key: fact:item, label: \"Item\", type: item, kind: item }",
        "  - { key: fact:request, label: \"Request\", type: text, kind: text }",
        "initial: run",
        "states:",
        "  run: { kind: wait, timeout: { after: 2d, then: park },",
        "         waitFor: { child: { recipe: workflow.t-agent-child, inputs: { \"fact:request\": { fact: fact:request } }, onParked: propagate } },",
        "         next: \"Waiting for {child}.\" }",
        "  done: { kind: terminal, outcome: resolved }",
        "transitions:",
        "  - { from: run, to: done, on: wait.satisfied, when: { outcome: resolved } }");

    private static readonly string _workspaceChild = _child
        .Replace("workflow.t-agent-child", "workflow.t-ws-child", StringComparison.Ordinal)
        .Replace("  - { key: fact:request, label: \"Request\", type: text, kind: text }", "  - { key: fact:request, label: \"Request\", type: text, kind: text }\n  - { key: fact:workspace, label: \"Phase worktree\", type: text, kind: text }", StringComparison.Ordinal);

    private static readonly string _workspaceParent = _parent
        .Replace("workflow.t-agent-parent", "workflow.t-ws-parent", StringComparison.Ordinal)
        .Replace("workflow.t-agent-child", "workflow.t-ws-child", StringComparison.Ordinal)
        .Replace("  - { key: fact:request, label: \"Request\", type: text, kind: text }", "  - { key: fact:request, label: \"Request\", type: text, kind: text }\n  - { key: fact:workspace, label: \"Phase worktree\", type: text, kind: text }", StringComparison.Ordinal)
        .Replace("inputs: { \"fact:request\": { fact: fact:request } }", "inputs: { \"fact:request\": { fact: fact:request }, \"fact:workspace\": { fact: fact:workspace } }", StringComparison.Ordinal);
}
