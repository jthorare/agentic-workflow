using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Skills;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Skills;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// How a skill Step's run ends in the interpreter (skill flows spec §3.3, #2191): a session that ends over a result file in the run directory, read by the real watch, through 'SkillEnded'.
/// A finished result's file sha lands on the recorded transition; a block-author bug fails the Step with its SND-SKILL code and the reason without it; a result that stays unreadable
/// until the Step's timeout is named in what the timeout says. The skill is the trial recipe's, on the fake launcher and sessions.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class SkillRunEndingTests : AgentRuntimeTestBase
{
    private readonly string _runs = Directory.CreateTempSubdirectory("skill-ending-").FullName;
    private readonly FakeLaunchFiles _files = new();
    private bool _fakeFiles;

    private string Findings => Path.Combine(_runs, "findings.json");

    [Fact(Timeout = 180_000)]
    public async Task A_finished_skill_records_its_result_files_sha_on_the_transition_not_the_report_digest()
    {
        var ct = TestContext.Current.CancellationToken;
        var environment = await WorkflowsHarness.StartDevServer(ct: ct);
        try
        {
            await Trial(environment, static text => text, async (sindri, handle, launch) =>
            {
                var bytes = Encoding.UTF8.GetBytes(Review("PASS"));
                Result(launch, bytes);
                _sessions.End(launch, SessionEndReason.Exit, exitCode: 0);

                var ended = await WorkflowsHarness.Until(handle, static v => v.State == "ended-pass");
                var wanted = Convert.ToHexStringLower(SHA256.HashData(bytes));
                var rows = await Rows(sindri, EventTypes.WorkflowTransition, static r => r.Exists(static p => p.Contains("resultSha256", StringComparison.OrdinalIgnoreCase)), ct);

                Assert.Equal("ended-pass", ended.State);
                Assert.Equal([wanted], rows.SelectMany(static p => Values(p, "resultSha256")), StringComparer.Ordinal);
                Assert.DoesNotContain(wanted, rows.SelectMany(static p => Values(p, "reportSha256")), StringComparer.Ordinal);   // why: the report digest also covers the outputs, so the two shas differ
            }, ct);
        }
        finally
        {
            await environment.ShutdownAsync();
        }
    }

    [Fact(Timeout = 240_000)]
    public async Task A_skill_failure_with_a_code_parks_the_Step_with_that_code_and_the_reason_without_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var environment = await WorkflowsHarness.StartDevServer(ct: ct);
        try
        {
            // why: with no transition on step.failed, a failed Step runs again twice and then parks, carrying the failure's code (Unavailables.Park)
            await Trial(environment, static text => WithoutFailedEnd(text), async (_, handle, _) =>
            {
                for (var n = 1; n <= 3; n++)
                {
                    await WaitFor(() => _launcher.Requests.Select(static r => r.Launch).Distinct().Take(n).Count() >= n, ct);
                    var launch = _launcher.Requests.Select(static r => r.Launch).Distinct().ElementAt(n - 1);
                    Result(launch, Encoding.UTF8.GetBytes(Review("MAYBE")));   // a verdict the block doesn't map: SND-SKILL-003
                    _sessions.End(launch, SessionEndReason.Exit, exitCode: 0);
                }

                var parked = await WorkflowsHarness.Until(handle, static v => v.State == "parked");

                Assert.Equal("SND-SKILL-003", parked.Question?.Code);
                Assert.Contains("review said 'MAYBE'", parked.Question?.Question, StringComparison.Ordinal);
                Assert.DoesNotContain("SND-SKILL-003", parked.Question?.Question, StringComparison.Ordinal);
            }, ct);
        }
        finally
        {
            await environment.ShutdownAsync();
        }
    }

    // RED until the timeout says why (#2191 comment): the watch records 'launch.owner-waiting' reason 'unreadable' and goes on, and when the Step's own timeout then fires the escalation says only '<state> ran past 1m'.
    // Real time: the time-skipping server never finishes a skip while the skill watch (a long activity) is in flight, and a recipe duration is at least 1m.
    [Fact(Timeout = 180_000)]
    public async Task A_result_that_stays_unreadable_until_the_timeout_is_named_in_what_the_timeout_says()
    {
        var ct = TestContext.Current.CancellationToken;
        _fakeFiles = true;
        var environment = await WorkflowsHarness.StartDevServer(ct: ct);
        try
        {
            await Trial(environment, static text => ShortEscalation(text), async (sindri, handle, launch) =>
            {
                _files.EndTurn(launch, RealTime.NowOffset - TimeSpan.FromMinutes(10));   // why: settled long ago on the host's clock, so the watch reads the result at once
                Result(launch, Encoding.UTF8.GetBytes("{\"skill\":\"review\",\"verdict\":"));   // cut off mid-write: not JSON
                var waiting = await Rows(sindri, EventTypes.LaunchOwnerWaiting, static r => r.Exists(static p => p.Contains("unreadable", StringComparison.Ordinal)), ct);
                Assert.Contains(waiting, static p => p.Contains("unreadable", StringComparison.Ordinal));   // the watch saw it and kept watching

                await Task.Delay(TimeSpan.FromSeconds(50), TimeProvider.System, ct);   // why: Until waits only 20 s, and the 1m timeout is due in about that long
                var escalated = await WorkflowsHarness.Until(handle, static v => v.State == "escalated");

                Assert.Contains("unreadable", escalated.Question?.Question, StringComparison.OrdinalIgnoreCase);
            }, ct);
        }
        finally
        {
            await environment.ShutdownAsync();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Directory.Delete(_runs, recursive: true);
        }

        base.Dispose(disposing);
    }

    // The trial's recipe with the states the rows need, started on the review block; <body> gets the host, the instance and the first run's launch once that has launched.
    private async Task Trial(WorkflowEnvironment environment, Func<string, string> recipe, Func<IServiceProvider, WorkflowHandle<RecipeWorkflow, RecipeResult>, LaunchId, Task> body, CancellationToken ct)
    {
        await File.WriteAllTextAsync(Findings, "{}", ct);
        var block = SkillManifestLoader.Load(SkillFixtures.Folder).Loaded.Single(static b => string.Equals(b.Skill, "review", StringComparison.Ordinal));
        var plan = SkillTrialRecipe.Build(block, provided: [], foldered: false).Value;
        var item = ItemId.From(_item.Id);
        _launcher.RunDirectory = _runs;
        _harness.Replacing = services => Fakes(services, item);
        _harness.Recipe(plan.Id.Value, recipe(plan.Text), services => services.AddSingleton(block.Descriptor));
        await using var sindri = _harness.Sindri(services =>
        {
            Fakes(services, item);
            _ = services.AddSingleton(block.Descriptor);
            _ = services.AddSingleton<ISkillBlocks>(new ServedSkills());
        });
        await WorkflowsHarness.HoldLease(sindri, ct);
        await AgentRig.ApproveTarget(sindri, ct);
        using var worker = new TemporalWorker(environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(async () =>
        {
            var start = await WorkflowsHarness.RecipeStart(sindri, plan.Id.Value, item.Value, ct);
            var handle = await WorkflowsHarness.Start(environment.Client, start);
            var running = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "run");
            var address = start.WorkflowId();
            var run = new AgentStepRun(WorkflowInstanceId.Of(address), address, running.Run!.Value, new StepRef("run", block.Descriptor.Id.Value), WriteScope: true);
            _diff.Add(AgentLaunches.WorktreeFor(run, new AgentStepTarget(item, AgentId.From("claude-code"), Repo)));
            await Launched(ct);
            await body(sindri, handle, AgentLaunches.LaunchIdFor(run));
        }, ct);
    }

    private void Fakes(IServiceCollection services, ItemId item)
    {
        _ = services.Replace(ServiceDescriptor.Singleton<ILauncher>(_launcher));
        _ = services.Replace(ServiceDescriptor.Singleton<IAgentSessions>(_sessions));
        _ = services.Replace(ServiceDescriptor.Singleton<IRunDiff>(_diff));
        _ = services.Replace(ServiceDescriptor.Singleton<IStepOutputs>(_outputs));
        _ = services.Replace(ServiceDescriptor.Singleton(new AgentRuntimeOptions(PollInterval: TimeSpan.FromMilliseconds(100)) { SlotRetry = TimeSpan.FromMilliseconds(50), SlotSlice = TimeSpan.FromMilliseconds(400) }));
        _ = services.Replace(ServiceDescriptor.Singleton<IItemRepos>(new KnownItemRepo(item)));
        if (_fakeFiles)
        {
            _ = services.Replace(ServiceDescriptor.Singleton<ILaunchFiles>(_files));   // why: the turn end's age is the fake's to set; the real hook marker would need 30 s of the host's clock
        }
    }

    private string Review(string verdict) => $$"""{"skill":"review","verdict":"{{verdict}}","review_file":{{JsonSerializer.Serialize(Findings)}}}""";

    private void Result(LaunchId launch, byte[] bytes)
    {
        var path = SkillLaunchEnv.ResultFileOf(_runs, launch);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    // The skill state's timeout cut to the shortest a recipe allows, and escalating at once instead of retrying first.
    private static string ShortEscalation(string text)
    {
        var shorter = text.Replace("timeout: { after: 2h, then: park }", "timeout: { after: 1m, then: escalate }", StringComparison.Ordinal);
        Assert.False(string.Equals(text, shorter, StringComparison.Ordinal), "the trial recipe has no 2h park timeout to shorten");
        return shorter;
    }

    // The trial recipe with no 'failed' end, so a failed Step is not taken by a transition.
    private static string WithoutFailedEnd(string text)
    {
        var without = text.Replace("  failed: { kind: terminal, outcome: failed }\n", string.Empty, StringComparison.Ordinal).Replace("  - { from: run, to: failed, on: step.failed }\n", string.Empty, StringComparison.Ordinal);
        Assert.False(string.Equals(text, without, StringComparison.Ordinal), "the trial recipe has no failed end to remove");
        return without;
    }

    // The payloads of the ledger's <type> rows once <ready> holds for them; whatever is there after 10 s otherwise.
    private static async Task<List<string>> Rows(IServiceProvider sindri, string type, Func<List<string>, bool> ready, CancellationToken ct)
    {
        List<string> rows = [];
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var read = (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, ct)).Value;
            rows = [.. read.Where(e => string.Equals(e.Envelope.Type, type, StringComparison.Ordinal)).Select(static e => e.Envelope.Payload)];
            if (ready(rows))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
        }

        return rows;
    }

    // Every string value of a property called <name> anywhere in a JSON payload.
    private static List<string> Values(string json, string name)
    {
        using var document = JsonDocument.Parse(json);
        List<string> found = [];
        Walk(document.RootElement, name, found);
        return found;
    }

    private static void Walk(JsonElement element, string name, List<string> found)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
                {
                    found.Add(property.Value.GetString()!);
                }

                Walk(property.Value, name, found);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Walk(item, name, found);
            }
        }
    }

    // The skills serve loaded: the fixture folder, whose 'review' is the descriptor registered as served.
    private sealed class ServedSkills : ISkillBlocks
    {
        public string Folder => SkillFixtures.Folder;

        public IReadOnlyList<LoadedSkillBlock> Loaded => SkillManifestLoader.Load(SkillFixtures.Folder).Loaded;

        public IReadOnlyList<SkippedSkillBlock> Skipped => [];

        public SkillBlocks LoadFolder(string folder) => new([], []);
    }

    private sealed class KnownItemRepo(ItemId known) : IItemRepos
    {
        public ValueTask<Kernel.Result<RepoName?>> RepoOf(ItemId item, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Kernel.Result<RepoName?>>(item == known ? RepoName.From(_item.Repo) : (RepoName?)null);

        public ValueTask<Kernel.Result<bool>> Exists(ItemId item, CancellationToken cancellationToken) => ValueTask.FromResult<Kernel.Result<bool>>(item == known);
    }
}
