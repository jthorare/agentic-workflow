using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Skills;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Skills;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A skills-folder trial in the interpreter (#2148 bug 23): its skill Step runs on the agent runtime (fake launcher and sessions); a folder block that maps an outcome serve's block doesn't is taken and ends, and a trial history, with or without the folder, replays.</summary>
[Lane(TestLane.Integration)]
public sealed class TrialInterpreterTests : AgentRuntimeTestBase
{
    private const string Review = "schema: 1\nblocks:\n  - id: review\n    sentence: Reviews a pull request.\n    inputs:  { pr: { type: text, label: Pull request (number or link), required: true } }\n    args: [\"{pr}\"]\n    outputs: { findings: { type: path, from: review_file } }\n    outcomes:\n      from: verdict\n      map: { PASS: pass }\n      success: pass\n    pushes: false\n    maxDuration: 2h\n";

    [Fact(Timeout = 180_000)]
    public async Task A_widened_map_folder_trial_takes_its_extra_outcome_and_ends()
    {
        using var folder = Folder(Review.Replace("{ PASS: pass }", "{ PASS: pass, BLOCKED: blocked }", StringComparison.Ordinal));

        var view = await RunTrial(folder.Path, foldered: true, outcome: "blocked", TestContext.Current.CancellationToken);

        Assert.Equal("ended-blocked", view.State);
    }

    [Fact(Timeout = 180_000)]
    public async Task A_trial_with_no_folder_runs_and_its_history_replays()
    {
        using var folder = Folder(Review);

        var view = await RunTrial(folder.Path, foldered: false, outcome: "pass", TestContext.Current.CancellationToken);

        Assert.Equal("ended-pass", view.State);
    }

    // Approves the trial of the folder's block against the folder's descriptor, serves the narrower fixture 'review', runs the trial's skill Step on the fake agent to <outcome> and replays its history.
    private async Task<InstanceView> RunTrial(string folder, bool foldered, string outcome, CancellationToken ct)
    {
        var block = SkillManifestLoader.Load(folder).Loaded.Single();
        var plan = SkillTrialRecipe.Build(block, provided: [], foldered).Value;
        var item = ItemId.From(_item.Id);
        var served = SkillManifestLoader.Load(SkillFixtures.Folder).Loaded.Single(static b => b.Skill == "review").Descriptor;
        _harness.Replacing = services => Fakes(services, item);
        _harness.Recipe(plan.Id.Value, plan.Text, services => services.AddSingleton(block.Descriptor));   // the approval pins the folder's block

        var environment = await WorkflowsHarness.StartDevServer(ct: ct);
        try
        {
            await using var sindri = _harness.Sindri(services =>
            {
                Fakes(services, item);
                _ = services.AddSingleton(served);   // serve's own copy of the block
            });
            await WorkflowsHarness.HoldLease(sindri, ct);
            await AgentRig.ApproveTarget(sindri, ct);
            using var worker = new TemporalWorker(environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
            InstanceView? ended = null;
            await worker.ExecuteAsync(async () => { ended = await RunToOutcome(sindri, environment, plan, block, item, (folder, foldered), outcome, ct); }, ct);
            return ended!;   // why: the body ran to its Until, which fails the test when the state is never reached
        }
        finally
        {
            await environment.ShutdownAsync();
        }
    }

    // The fakes the agent rig runs on, plus the item's repo (a trial has no fact:item, so the address names it).
    private void Fakes(IServiceCollection services, ItemId item)
    {
        _ = services.Replace(ServiceDescriptor.Singleton<ILauncher>(_launcher));
        _ = services.Replace(ServiceDescriptor.Singleton<IAgentSessions>(_sessions));
        _ = services.Replace(ServiceDescriptor.Singleton<IRunDiff>(_diff));
        _ = services.Replace(ServiceDescriptor.Singleton<IStepOutputs>(_outputs));
        _ = services.Replace(ServiceDescriptor.Singleton(new AgentRuntimeOptions(PollInterval: TimeSpan.FromMilliseconds(100)) { SlotRetry = TimeSpan.FromMilliseconds(50), SlotSlice = TimeSpan.FromMilliseconds(400) }));
        _ = services.Replace(ServiceDescriptor.Singleton<IItemRepos>(new KnownItemRepo(item)));
    }

    // Starts the trial, lets its skill Step launch on the fake agent, reports <outcome> with the block's outputs (none for a blocked one), replays the history and, for a foldered run, records it when AW_TRIAL_HISTORY_OUT is set.
    private async Task<InstanceView> RunToOutcome(IServiceProvider sindri, WorkflowEnvironment environment, SkillTrialPlan plan, LoadedSkillBlock block, ItemId item, (string Path, bool Foldered) folder, string outcome, CancellationToken ct)
    {
        var start = await WorkflowsHarness.RecipeStart(sindri, plan.Id.Value, item.Value, ct);
        var inputs = folder.Foldered ? new Dictionary<FactKey, InputFact> { [SkillLaunches.TrialFact] = new InputFact("skills", _harness.Fact(folder.Path)) } : [];
        start = start with { Inputs = inputs };
        var handle = await WorkflowsHarness.Start(environment.Client, start);
        var running = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "run");
        var address = start.WorkflowId();
        var run = new AgentStepRun(WorkflowInstanceId.Of(address), address, running.Run!.Value, new StepRef("run", block.Descriptor.Id.Value), WriteScope: true);
        _diff.Add(AgentLaunches.WorktreeFor(run, new AgentStepTarget(item, AgentId.From("claude-code"), Repo)));
        await LaunchedOrReport(sindri, handle, ct);

        var outputs = block.Descriptor.Outputs.Where(_ => outcome != "blocked").ToDictionary(static o => o.Fact, static _ => new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null));
        var report = new StepCompleted("agent:session", running.Run.Value, OutcomeName.From(outcome), outputs, Tokens: 0, StepRuntime.Agent, new string('f', 64));
        _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('a', 32)));
        var ended = await WorkflowsHarness.Until(handle, static v => SkillTrialRecipe.OutcomeOf(v.State) is not null);

        var history = await handle.FetchHistoryAsync();
        if (folder.Foldered && Environment.GetEnvironmentVariable("AW_TRIAL_HISTORY_OUT") is { Length: > 0 } path)
        {
            await File.WriteAllTextAsync(path, history.ToJson(), ct);   // records the fixture trial-prefolder-history.json on the code under test
        }

        await AgentRig.Replayed(handle);
        return ended;
    }

    // why: a launch that never comes is a silent 30 s wait; the instance and the ledger say what the Step did instead
    private async Task LaunchedOrReport(IServiceProvider sindri, WorkflowHandle<RecipeWorkflow, RecipeResult> handle, CancellationToken ct)
    {
        try
        {
            await Launched(ct);
        }
        catch (Xunit.Sdk.TrueException)
        {
            var view = await handle.QueryAsync(static wf => wf.GetInstance());
            var rows = (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 500, ct)).Value;
            var lines = rows.Select(static r => r.Envelope.Type + " " + r.Envelope.Payload);
            Assert.Fail("no launch. state=" + view.State + " question=" + view.Question + "\n" + string.Join('\n', lines));
        }
    }

    private sealed class KnownItemRepo(ItemId known) : IItemRepos
    {
        public ValueTask<Result<RepoName?>> RepoOf(ItemId item, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<RepoName?>>(item == known ? RepoName.From(_item.Repo) : (RepoName?)null);

        public ValueTask<Result<bool>> Exists(ItemId item, CancellationToken cancellationToken) => ValueTask.FromResult<Result<bool>>(item == known);
    }

    private static TempFolder Folder(string yaml)
    {
        var folder = new TempFolder();
        Directory.CreateDirectory(Path.Combine(folder.Path, "review"));
        File.WriteAllText(Path.Combine(folder.Path, "review", "block.yaml"), yaml);
        File.Copy(Path.Combine(SkillFixtures.Folder, "review", "SKILL.md"), Path.Combine(folder.Path, "review", "SKILL.md"));
        return folder;
    }

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("trial-run").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
