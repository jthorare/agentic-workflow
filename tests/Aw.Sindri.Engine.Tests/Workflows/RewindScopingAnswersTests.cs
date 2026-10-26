using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Scoping;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Rewinding back to the interview on the real workflow (#1270): the interview of workflow.scoping on its own (the fixture recipe holds the real
/// 'step.interview.answer' and 'step.interview.record' Steps and a desk Step after them). Batch 1 is recorded as A1 and the instance reaches the desk Step; the
/// 'rewind' Update sends it back to the interview with a new run and the same loop round; a second batch that supersedes A1, reported through the same
/// 'complete' Update 'answer --record' makes, is recorded as A2 with its 'supersedes' carried, and the instance is at the desk Step again.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class RewindScopingAnswersTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private const string Batch1 = "[{\"batch\":1,\"round\":1,\"actor\":\"owner\",\"questionIds\":[\"Q1\"],\"recordedAs\":[\"AC-1\"],\"supersedes\":[],\"answers\":[{\"question\":\"Q1\",\"option\":\"Q1.a\",\"text\":\"Add a choice\"}]}]";
    private const string Batch2 = "[{\"batch\":2,\"round\":2,\"actor\":\"owner\",\"questionIds\":[\"Q1\"],\"recordedAs\":[\"AC-1\"],\"supersedes\":[\"A1\"],\"answers\":[{\"question\":\"Q1\",\"option\":\"Q1.b\",\"text\":\"Never expire\"}]}]";

    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 180_000)]
    public async Task A_rewind_back_to_interview_records_a_batch_that_supersedes_A1_as_A2_and_is_not_a_loop_round()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.test-rewind-answers", await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Workflows", "Fixtures", "workflow.test-rewind-answers.yaml"), ct));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        var store = sindri.GetRequiredService<IFactStore>();
        var (first, second) = (await Put(store, Batch1, ct), await Put(store, Batch2, ct));

        await worker.ExecuteAsync(async () =>
        {
            var handle = await Started(sindri, store, ct);
            var asking = await WorkflowsHarness.Until(handle, static v => v.State == "interview" && v.Run is not null);
            _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(Answered(asking, first)), new WorkflowUpdateOptions(new string('1', 32)));
            var holding = await WorkflowsHarness.Until(handle, static v => v.State == "hold" && v.Run is not null);
            var firstLog = await AnswerLog(sindri, handle, ct);

            var back = await handle.ExecuteUpdateAsync(wf => wf.RewindAsync(new RewindRequested("human:cli", Stamp('2'), "interview")), new WorkflowUpdateOptions(new string('2', 32)));
            var again = await WorkflowsHarness.Until(handle, static v => v.State == "interview" && v.Run is not null);
            _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(Answered(again, second)), new WorkflowUpdateOptions(new string('3', 32)));
            var held = await WorkflowsHarness.Until(handle, static v => v.State == "hold" && v.Run?.Value == "6.1");
            var log = await AnswerLog(sindri, handle, ct);

            Assert.Equal("1.1", asking.Run?.Value);
            Assert.Equal(["A1"], Ids(firstLog));
            Assert.Equal("3.1", holding.Run?.Value);
            Assert.Equal(("interview", "4.1"), (back.Instance.State, back.Instance.Run?.Value));   // a new run: the hold's 3.1 is gone
            Assert.Contains("round 1/3", asking.Next, StringComparison.Ordinal);
            Assert.Contains("round 1/3", again.Next, StringComparison.Ordinal);   // a rewind is not a loop round
            Assert.Equal(["A1", "A2"], Ids(log));
            Assert.Equal(["A1"], [.. log[1]!["supersedes"]!.AsArray().Select(static s => s!.GetValue<string>())]);
            Assert.Equal(("hold", "6.1"), (held.State, held.Run?.Value));
            await handle.TerminateAsync("test over");
        }, ct);
    }

    private async Task<WorkflowHandle<RecipeWorkflow, RecipeResult>> Started(IServiceProvider sindri, IFactStore store, CancellationToken ct)
    {
        var approved = await sindri.GetRequiredService<IApprovedRecipes>().Latest(RecipeId.From("workflow.test-rewind-answers"), ct);
        var inputs = new Dictionary<FactKey, InputFact>
        {
            [FactKey.From("fact:questions")] = new InputFact("questions.json", (await store.Put(Encoding.UTF8.GetBytes(ScopingFacts.Expiry()["questions"]), ct)).Value),
            [FactKey.From("fact:answerLog")] = new InputFact("answerLog.json", (await store.Put("[]"u8.ToArray(), ct)).Value),
        };
        var start = new InstanceStart(approved.Value.Version, ItemKey.From("T-1270"), Attempt: 1, inputs, Tenant: "local", Ring: 0) { StartedAt = RealTime.NowOffset };
        return await WorkflowsHarness.Start(server.Environment.Client, start);
    }

    private static string[] Ids(JsonArray log) => [.. log.Select(static e => e!["id"]!.GetValue<string>())];

    private static async Task<FactValue> Put(IFactStore store, string text, CancellationToken ct) => (await store.Put(Encoding.UTF8.GetBytes(text), ct)).Value;

    private static StepCompleted Answered(InstanceView view, FactValue batch) => new(
        "human:cli", view.Run!.Value, OutcomeName.From("answered"), new Dictionary<FactKey, FactValue> { [FactKey.From("fact:answers")] = batch },
        Tokens: 0, StepRuntime.Desk, new string('f', 64));

    /// <summary>The stored 'fact:answerLog', read through the instance's fact hashes and the fact contents, as the facts route does.</summary>
    private static async Task<JsonArray> AnswerLog(IServiceProvider sindri, WorkflowHandle<RecipeWorkflow, RecipeResult> handle, CancellationToken ct)
    {
        var hash = (await handle.QueryAsync(static wf => wf.GetInstance())).FactShas!["fact:answerLog"];   // the fact query GetInstanceFact reads: the row's own ref is null for a payload
        var content = await sindri.GetRequiredService<IFactContents>().Read(new FactValue("fact:sha256:" + hash, hash, HeadSha: null), ct);
        return (JsonArray)JsonNode.Parse(content.Value.Span)!;
    }

    private static CommandStamp Stamp(char digit) => new(CommandNonce.From(new string(digit, 32)), new string('0', 64));
}
