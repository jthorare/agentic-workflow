using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Skills;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Skills;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A skills-folder trial in the interpreter (#2148 bug 23): a folder block that maps an outcome serve's block doesn't is taken and ends, and a trial history, with or without the folder, replays.</summary>
[Lane(TestLane.Integration)]
public sealed class TrialInterpreterTests(BlockRuntimeDevServer server) : IClassFixture<BlockRuntimeDevServer>, IDisposable
{
    private const string Review = "schema: 1\nblocks:\n  - id: review\n    sentence: Reviews a pull request.\n    inputs:  { pr: { type: text, label: Pull request (number or link), required: true } }\n    args: [\"{pr}\"]\n    outputs: { findings: { type: path, from: review_file } }\n    outcomes:\n      from: verdict\n      map: { PASS: pass }\n      success: pass\n    pushes: false\n    maxDuration: 2h\n";
    private readonly WorkflowsHarness _harness = new();
    private static int _items;

    public void Dispose() => _harness.Dispose();

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

    // Registers and approves the trial of the folder's block against the folder's descriptor, serves the narrower fixture 'review', runs the trial to <outcome> and replays its history.
    private async Task<InstanceView> RunTrial(string folder, bool foldered, string outcome, CancellationToken ct)
    {
        var block = SkillManifestLoader.Load(folder).Loaded.Single();
        var plan = SkillTrialRecipe.Build(block, provided: [], foldered).Value;
        _harness.Recipe(plan.Id.Value, plan.Text, services => services.AddSingleton(block.Descriptor));   // the approval pins the folder's block
        var served = SkillManifestLoader.Load(SkillFixtures.Folder).Loaded.Single(static b => b.Skill == "review").Descriptor;
        var outputs = block.Descriptor.Outputs.Where(_ => outcome != "blocked").ToDictionary(static o => o.Fact, static _ => JsonSerializer.SerializeToElement("/tmp/findings.json"));
        var step = new FakeBlockStep((_, _) => ValueTask.FromResult(BlockStepResult.Completed(OutcomeName.From(outcome), outputs)));   // why: the harness has no agent runtime for a skill Step, so a registered block runtime stands in for the session
        await using var sindri = _harness.Sindri(services =>
        {
            _ = services.AddSingleton(served);   // serve's own copy of the block
            _ = services.Replace(ServiceDescriptor.Singleton(new BlockStepRuntimes([new BlockStepRegistration(served.Id)])));
            _ = services.AddKeyedSingleton<IBlockStep>(served.Id.Value, step);
        });
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        InstanceView? ended = null;
        await worker.ExecuteAsync(async () =>
        {
            var start = await WorkflowsHarness.RecipeStart(sindri, plan.Id.Value, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"T-{Interlocked.Increment(ref _items)}"), ct);
            var inputs = foldered ? new Dictionary<FactKey, InputFact> { [SkillLaunches.TrialFact] = new InputFact("skills", _harness.Fact(folder)) } : [];
            var handle = await WorkflowsHarness.Start(server.Environment.Client, start with { Inputs = inputs });
            ended = await WorkflowsHarness.Until(handle, v => SkillTrialRecipe.OutcomeOf(v.State) is not null);
            var history = await handle.FetchHistoryAsync();
            if (foldered && Environment.GetEnvironmentVariable("AW_TRIAL_HISTORY_OUT") is { Length: > 0 } path)
            {
                await File.WriteAllTextAsync(path, history.ToJson(), ct);   // records the fixture trial-prefolder-history.json on the code under test
            }

            var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>()).ReplayWorkflowAsync(history, throwOnReplayFailure: false, ct);
            Assert.Null(replayed.ReplayFailure);
        }, ct);
        return ended!;   // why: the body ran to its Until, which fails the test when the state is never reached
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
