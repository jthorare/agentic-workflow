using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.ActivateWorkflow;
using Aw.Sindri.Features.CreateWorkflow;
using Aw.Sindri.Features.SetupWatch;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Launch;
using Aw.Sindri.Tests.WorkflowWrites;

namespace Aw.Sindri.Tests.Setup;

/// <summary>The setup service's first-run steps (first-run spec E1 and E2): the agent target from the first agent the launcher finds, and hello-sindri made and activated once when the profile first names a repo.</summary>
[Lane(TestLane.Unit)]
public sealed class FirstRunSetupTests : IDisposable
{
    private static readonly WorkflowSlug _hello = WorkflowSlug.From("hello-sindri");

    private readonly WorkflowWriteRig _rig = new(realBlocks: true, flowAndCodeBlocks: true);
    private readonly RecordingKeys _keys = new();
    private readonly RecordingRunner _runner = new();
    private Result<LaunchShowReport> _agents = Report(("claude-code", "/usr/bin/claude"));

    public void Dispose() => _rig.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Result<LaunchShowReport> Report(params (string Id, string? Bin)[] agents) =>
        new LaunchShowReport("none", "default", "/home/launcher.json", [.. agents.Select(static a => new LaunchShowEntry(AgentId.From(a.Id), CatalogSource.Builtin, a.Id, a.Bin, ArgvHash: null, StepOk: true, ParkLine: null))], []);

    private void Profile(SetupSettings? setup = null, string? target = null, params string[] repos)
    {
        var profile = ProfileRules.Check(new SindriProfile(
            1,
            "thor",
            Repos: [.. repos.Select(static r => new RepoEntry(r, "/work/" + r))],
            Workflow: target is null ? null : new WorkflowSettings(Agent: new WorkflowAgentSettings(AgentId.From(target))),
            Setup: setup)).Value;
        _rig.Harness.Profiles.Live = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }

    private FirstRunSetup Setup(InMemoryWorkflowStore? workflows = null)
    {
        var store = workflows ?? _rig.Workflows;
        return new FirstRunSetup(
            _rig.Harness.Profiles,
            new ScriptedLaunchSetup(SindriErrors.NotRunning.With("not used"), _agents),
            _keys,
            Create(store),
            Activate(store),
            _runner,
            new FirstWorkflowEvents(_rig.Harness.Held, _rig.Harness.Store, _rig.Harness.Store, _rig.Harness.Events, _rig.Harness.Callers));
    }

    // the same services the slice ports wrap, called directly (no command chain in a unit test)
    private Creator Create(InMemoryWorkflowStore store) => new(new CreateWorkflowService(store, _rig.Checker, _rig.Harness.Ids, _rig.Log));

    private Activator Activate(InMemoryWorkflowStore store) => new(new ActivateWorkflowService(store, _rig.Checker, _rig.Log));

    private async Task<IReadOnlyList<Core.Graph.WorkflowRecord>> Made(InMemoryWorkflowStore? workflows = null)
    {
        var listed = (await (workflows ?? _rig.Workflows).List(includeArchived: true, Token)).Value;
        return [.. listed.Where(static w => w.Slug == _hello)];
    }

    [Fact]
    public async Task Setup_creates_and_activates_hello_sindri_when_the_first_repo_is_added()
    {
        _ = await _rig.Harness.HoldLease();
        Profile(repos: "demo");

        await Setup().Run(Token);

        var record = Assert.Single(await Made());
        var active = Assert.NotNull(record.ActiveRevision);
        var spec = (await _rig.Workflows.GetRevision(record.Id, active, Token)).Value.Spec;
        Assert.Equal("demo", spec.Nodes.Single(static n => n.Type == "create_item").Config["repo"]!.GetValue<string>());
        var created = Assert.Single(_rig.Recorded(EventTypes.SetupFirstWorkflowCreated));
        Assert.Equal("setup.first_workflow.created", created.IdempotencyKey);
    }

    [Fact]
    public async Task Setup_runs_hello_sindri_once_after_activating_it()
    {
        _ = await _rig.Harness.HoldLease();
        Profile(repos: "demo");
        var setup = Setup();

        await setup.Run(Token);
        await setup.Run(Token);

        Assert.Equal(["hello-sindri"], _runner.Started);
    }

    [Fact]
    public async Task A_hello_sindri_the_owner_deleted_is_not_run_again()
    {
        _ = await _rig.Harness.HoldLease();
        Profile(repos: "demo");
        await Setup().Run(Token);

        await Setup(new InMemoryWorkflowStore(_rig.Harness.Clock)).Run(Token);

        _ = Assert.Single(_runner.Started);
    }

    [Fact]
    public async Task A_second_pass_does_not_make_hello_sindri_again()
    {
        _ = await _rig.Harness.HoldLease();
        Profile(repos: "demo");
        var setup = Setup();

        await setup.Run(Token);
        await setup.Run(Token);

        _ = Assert.Single(await Made());
        _ = Assert.Single(_rig.Recorded(EventTypes.SetupFirstWorkflowCreated));
    }

    [Fact]
    public async Task A_hello_sindri_the_owner_deleted_is_not_made_again()
    {
        _ = await _rig.Harness.HoldLease();
        Profile(repos: "demo");
        await Setup().Run(Token);
        var deleted = new InMemoryWorkflowStore(_rig.Harness.Clock);   // a store that no longer holds it, as after a delete

        await Setup(deleted).Run(Token);

        Assert.Empty(await Made(deleted));
        _ = Assert.Single(_rig.Recorded(EventTypes.SetupFirstWorkflowCreated));
    }

    [Fact]
    public async Task Nothing_is_made_while_the_profile_names_no_repo()
    {
        _ = await _rig.Harness.HoldLease();
        Profile();

        await Setup().Run(Token);

        Assert.Empty(await Made());
        Assert.Empty(_rig.Recorded(EventTypes.SetupFirstWorkflowCreated));
        Assert.Empty(_keys.Calls);
    }

    [Fact]
    public async Task A_dismissed_hello_sindri_is_not_made()
    {
        _ = await _rig.Harness.HoldLease();
        Profile(new SetupSettings { SkipFirstWorkflow = true }, repos: "demo");

        await Setup().Run(Token);

        Assert.Empty(await Made());
        Assert.Empty(_rig.Recorded(EventTypes.SetupFirstWorkflowCreated));
    }

    [Fact]
    public async Task The_agent_target_is_set_from_the_first_agent_the_launcher_finds()
    {
        _ = await _rig.Harness.HoldLease();
        Profile(repos: "demo");
        _agents = Report(("claude-code", null), ("codex", "/usr/bin/codex"), ("cursor", "/usr/bin/cursor-agent"));

        await Setup().Run(Token);

        Assert.Equal(["plan workflow.agent.target=codex", "save"], _keys.Calls);
    }

    [Fact]
    public async Task A_target_that_is_set_stays_and_no_agent_found_sets_nothing()
    {
        _ = await _rig.Harness.HoldLease();
        Profile(target: "codex", repos: "demo");
        await Setup().Run(Token);
        Profile(repos: "demo");
        _agents = Report(("claude-code", null));

        await Setup().Run(Token);

        Assert.Empty(_keys.Calls);
    }

    private sealed class RecordingRunner : ISetupWorkflowRunner
    {
        public List<string> Started { get; } = [];

        public ValueTask<Result<Unit>> Run(WorkflowSlug slug, CancellationToken cancellationToken)
        {
            Started.Add(slug.Value);
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
    }

    private sealed class Creator(CreateWorkflowService service) : ISetupWorkflowCreator
    {
        public async ValueTask<Result<Unit>> Create(WorkflowSlug slug, string spec, CancellationToken cancellationToken)
        {
            var made = await service.Handle(new CreateWorkflowRequest(slug, Template: null, spec, "service:setup"), cancellationToken);
            return made.IsError ? made.Error : Unit.Value;
        }
    }

    private sealed class Activator(ActivateWorkflowService service) : ISetupWorkflowActivator
    {
        public async ValueTask<Result<Unit>> Activate(WorkflowSlug slug, CancellationToken cancellationToken)
        {
            var made = await service.Handle(new ActivateWorkflowRequest(slug, Revision: null, "service:setup"), cancellationToken);
            return made.IsError ? made.Error : Unit.Value;
        }
    }

    private sealed class RecordingKeys : IProfileKeyWriter
    {
        public List<string> Calls { get; } = [];

        public Result<ProfileKeyEdit> Plan(string keyPath, string value) => throw new NotSupportedException();

        public Result<ProfileKeyEdit> PlanText(string keyPath, string value)
        {
            Calls.Add($"plan {keyPath}={value}");
            return new ProfileKeyEdit("/home/profile.yaml", "text", Changed: true);
        }

        public Result<ProfileKeyEdit> PlanTexts(IReadOnlyList<(string KeyPath, string Value)> values) => throw new NotSupportedException();

        public Result<ProfileKeyEdit> PlanRemove(string keyPath) => throw new NotSupportedException();

        public Result<string> Save(ProfileKeyEdit edit)
        {
            Calls.Add("save");
            return edit.Path;
        }
    }
}
