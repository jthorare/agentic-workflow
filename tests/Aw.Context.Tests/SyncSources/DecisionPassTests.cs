using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Models;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Features.SyncSources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Linking;
using Aw.Kernel;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.SyncSources;

/// <summary>The decision pass on its own: what it does with a record whose extraction can never fit 'maxTokensPerSync'.</summary>
[Trait("Lane", "unit")]
public sealed class DecisionPassTests
{
    private static readonly SourceId _linear = SourceId.From("linear");
    private static readonly RecordKey _big = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000201");
    private static readonly RecordKey _small = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000202");
    private static readonly DateTimeOffset _at = DateTimeOffset.Parse("2026-08-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PendingRecord Pending(RecordKey key, string text) =>
        new(new IntentRecord(key, _linear, "v1", "Title", []), text, [new IntentLink(key, NodeKey.From("feature:a"), LinkRule.IssueKey, 1.0, "ab12")]);

    [Fact]
    public async Task A_record_whose_prompt_alone_exceeds_the_cap_is_refused_and_not_queued_and_the_rest_is_still_extracted()
    {
        var store = new FakeIntentStore();
        var model = new FakeModelPort { Answer = """{"decisions":[]}""" };
        var records = new[] { Pending(_big, new string('x', 12_000)), Pending(_small, "short") };
        _ = await store.CommitPage(new IntentPage(_linear, [.. records.Select(r => r.Record)], [.. records.SelectMany(r => r.Links)], Cursor: null, _at), Ct);
        var pass = new DecisionPass(store, model, new LabelingSettings { Enabled = true, MaxTokensPerSync = 2_000 }, (new FakeScrubber(), new FakeTimeProvider(_at)));

        await pass.Run(records, Ct);

        var queued = (await store.Queued(_linear, Ct)).Value;
        Assert.Equal(1, pass.Refused);   // the oversized record, counted
        Assert.False(pass.HeldBack);   // it can never fit, so it neither waits nor stops the pass
        Assert.Empty(queued);
        Assert.Contains(_small.Value, model.Requests.Select(r => r.Prompt).Single(), StringComparison.Ordinal);   // the small one was still asked
        _ = Assert.Single(model.Requests);
    }

    private static readonly NodeKey _node = NodeKey.From("feature:a");

    private static async Task Commit(FakeIntentStore store, params PendingRecord[] records) =>
        _ = await store.CommitPage(new IntentPage(_linear, [.. records.Select(r => r.Record)], [.. records.SelectMany(r => r.Links)], Cursor: null, _at), Ct);

    /// <summary>A model that answers like the fake but reports <paramref name="spent"/> input tokens for every call.</summary>
    private sealed class CostlyModel(FakeModelPort inner, int spent) : IModelPort
    {
        public async ValueTask<Result<ModelAnswer>> Ask(ModelRequest request, CancellationToken cancellationToken)
        {
            var answer = await inner.Ask(request, cancellationToken);
            return answer.IsError ? answer : new ModelAnswer(answer.Value.Text, spent, 0);
        }
    }

    [Fact]
    public async Task A_record_whose_extraction_leaves_no_room_for_its_conflict_checks_gives_up_the_pairs_and_does_not_block_the_records_after_it()
    {
        const string quote = "Toggling keeps focus first";
        var store = new FakeIntentStore();
        var earlier = Pending(_small, "Another decision on the node");
        var first = Pending(_big, quote + ".");
        var later = Pending(RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000203"), "A later record");
        await Commit(store, earlier);
        var seeded = new DecisionRow(_small, "Another decision on the node", "Other", Provenance.Model, _at);
        _ = await store.SaveDecisions(new DecisionBatch(_small, "v1", [seeded], [new GraphEdge(seeded.ToKey(), _node, EdgeType.Decides, Provenance.Model, 1.0, At: _at)]), Ct);
        await Commit(store, first, later);
        var fake = new FakeModelPort { Answer = $$"""{"decisions":[{"quote":"{{quote}}","wording":"Keeps focus","decides":["feature:a"]}]}""" };
        var cap = 2_000;
        var pass = new DecisionPass(store, new CostlyModel(fake, cap - 20), new LabelingSettings { Enabled = true, MaxTokensPerSync = cap }, (new FakeScrubber(), new FakeTimeProvider(_at)));

        await pass.Run([first, later], Ct);

        var queued = (await store.Queued(_linear, Ct)).Value.Select(k => k.Value);
        var layer = (await store.Layer(Ct)).Value;
        Assert.DoesNotContain(_big.Value, queued, StringComparer.Ordinal);   // done: its checks can never fit, so they are given up, not retried forever ahead of the records after it
        Assert.Contains(later.Record.Key.Value, queued, StringComparer.Ordinal);   // held back this run, and now first in line
        Assert.Equal(ConflictVerdict.Refused, Assert.Single(layer.Conflicts).Verdict);
        Assert.Equal(1, pass.Refused);
        _ = Assert.Single(layer.Decisions, d => d.Record == _big);
    }

    [Theory]
    [InlineData("SND-CONTEXT-240")]
    [InlineData("SND-CONTEXT-241")]
    public async Task No_model_harness_to_ask_holds_the_record_back_and_is_no_failure(string code)
    {
        var store = new FakeIntentStore();
        var record = Pending(_small, "A decision");
        await Commit(store, record);
        var model = new FakeModelPort { Fail = _ => string.Equals(code, ContextErrors.NoModelHarness.Code, StringComparison.Ordinal) ? ContextErrors.NoModelHarness.With("none") : ContextErrors.ModelHarnessesUnavailable.With("limited") };
        var pass = new DecisionPass(store, model, new LabelingSettings { Enabled = true, MaxTokensPerSync = 2_000 }, (new FakeScrubber(), new FakeTimeProvider(_at)));

        await pass.Run([record], Ct);

        Assert.True(pass.HeldBack);
        Assert.Null(pass.Failure);
        Assert.Contains(_small.Value, (await store.Queued(_linear, Ct)).Value.Select(static k => k.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Any_other_model_error_still_fails_the_pass_with_its_code()
    {
        var store = new FakeIntentStore();
        var record = Pending(_small, "A decision");
        await Commit(store, record);
        var model = new FakeModelPort { Fail = _ => ContextErrors.LabelingUnavailable.With("nobody answered") };
        var pass = new DecisionPass(store, model, new LabelingSettings { Enabled = true, MaxTokensPerSync = 2_000 }, (new FakeScrubber(), new FakeTimeProvider(_at)));

        await pass.Run([record], Ct);

        Assert.Equal(ContextErrors.LabelingUnavailable.Code, pass.Failure);
    }

    [Fact]
    public async Task A_store_error_while_queuing_a_held_back_record_stops_the_pass_with_its_code()
    {
        var store = new FakeIntentStore { RequeueFailure = ContextErrors.StoreUnavailable.With("requeue failed") };
        var record = Pending(_big, "text");
        await Commit(store, record);
        var pass = new DecisionPass(store, new FakeModelPort(), new LabelingSettings { Enabled = false }, (new FakeScrubber(), new FakeTimeProvider(_at)));

        await pass.Run([record], Ct);

        Assert.Equal((true, ContextErrors.StoreUnavailable.Code), (pass.HeldBack, pass.Failure));
    }

    [Fact]
    public async Task A_store_error_while_giving_up_an_oversized_record_stops_the_pass_with_its_code()
    {
        var store = new FakeIntentStore { EmptyBatchFailure = ContextErrors.StoreUnavailable.With("save failed") };
        var model = new FakeModelPort { Answer = """{"decisions":[]}""" };
        var records = new[] { Pending(_big, new string('x', 12_000)), Pending(_small, "short") };
        await Commit(store, records);
        var pass = new DecisionPass(store, model, new LabelingSettings { Enabled = true, MaxTokensPerSync = 2_000 }, (new FakeScrubber(), new FakeTimeProvider(_at)));

        await pass.Run(records, Ct);

        Assert.Equal(ContextErrors.StoreUnavailable.Code, pass.Failure);
        Assert.Empty(model.Requests);   // the pass stopped: the record after it was held back, not asked
    }

    private const string Quote = "Toggling keeps focus first";

    private static string Extraction(string quote) => $$"""{"decisions":[{"quote":"{{quote}}","wording":"Keeps focus","decides":["feature:a"]}]}""";

    private static string Route(ModelRequest request, string quote) => request.Purpose == ModelPurpose.Adjudication ? """{"verdict":"agrees"}""" : Extraction(quote);

    /// <summary>A first record that decides on a node two seeded decisions already decide on, so it has two conflict pairs.</summary>
    private static async Task<(FakeIntentStore Store, PendingRecord Record)> TwoPairs()
    {
        var store = new FakeIntentStore();
        var record = Pending(_big, Quote + ".");
        foreach (var (key, wording) in new[] { (_small, "Another decision on the node"), (RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000204"), "Yet another decision") })
        {
            await Commit(store, Pending(key, wording));
            var seeded = new DecisionRow(key, wording, "Other", Provenance.Model, _at);
            _ = await store.SaveDecisions(new DecisionBatch(key, "v1", [seeded], [new GraphEdge(seeded.ToKey(), _node, EdgeType.Decides, Provenance.Model, 1.0, At: _at)]), Ct);
        }

        await Commit(store, record);
        return (store, record);
    }

    [Fact]
    public async Task A_first_record_with_one_pair_answered_and_the_next_over_the_cap_stays_queued_and_the_next_run_answers_the_rest()
    {
        var (store, record) = await TwoPairs();
        var model = new FakeModelPort { Responder = r => Route(r, Quote) };
        var settings = new LabelingSettings { Enabled = true, MaxTokensPerSync = 1_900 };
        var pass = new DecisionPass(store, new CostlyModel(model, 900), settings, (new FakeScrubber(), new FakeTimeProvider(_at)));

        await pass.Run([record], Ct);

        var layer = (await store.Layer(Ct)).Value;
        Assert.Contains(_big.Value, (await store.Queued(_linear, Ct)).Value.Select(k => k.Value), StringComparer.Ordinal);   // one pair is answered, so the rest is only waiting for room, not impossible
        Assert.Equal(ConflictVerdict.Agrees, Assert.Single(layer.Conflicts).Verdict);
        Assert.Equal((0, true), (pass.Refused, pass.HeldBack));

        var next = new DecisionPass(store, model, new LabelingSettings { Enabled = true, MaxTokensPerSync = 100_000 }, (new FakeScrubber(), new FakeTimeProvider(_at)));
        await next.Run([record], Ct);

        Assert.Empty((await store.Queued(_linear, Ct)).Value);
        var answered = (await store.Layer(Ct)).Value.Conflicts;
        Assert.Equal(2, answered.Count);   // the pair answered before stays, the other is answered now
        Assert.All(answered, c => Assert.Equal(ConflictVerdict.Agrees, c.Verdict));
        Assert.Equal(0, next.Refused);
    }

    [Fact]
    public async Task A_later_record_that_hits_the_cap_in_its_conflict_checks_stays_queued_and_is_not_given_up()
    {
        var store = new FakeIntentStore();
        var earlier = Pending(_small, "Alpha decision text.");
        var later = Pending(_big, "Beta decision text.");
        await Commit(store, earlier, later);
        var model = new FakeModelPort { Responder = r => Route(r, r.Prompt.Contains("Alpha decision", StringComparison.Ordinal) ? "Alpha decision text" : "Beta decision text") };
        var pass = new DecisionPass(store, new CostlyModel(model, 900), new LabelingSettings { Enabled = true, MaxTokensPerSync = 1_900 }, (new FakeScrubber(), new FakeTimeProvider(_at)));

        await pass.Run([earlier, later], Ct);

        Assert.Equal(_big, Assert.Single((await store.Queued(_linear, Ct)).Value));   // the cap was spent by the record before it, so a later run may well fit its checks
        Assert.Empty((await store.Layer(Ct)).Value.Conflicts);
        Assert.Equal((0, true), (pass.Refused, pass.HeldBack));
    }

    private static async Task<(FakeIntentStore Store, DecisionPass Pass, PendingRecord First)> NoRoom()
    {
        var store = new FakeIntentStore();
        var first = Pending(_big, Quote + ".");
        await Commit(store, Pending(_small, "Another decision on the node"));
        var seeded = new DecisionRow(_small, "Another decision on the node", "Other", Provenance.Model, _at);
        _ = await store.SaveDecisions(new DecisionBatch(_small, "v1", [seeded], [new GraphEdge(seeded.ToKey(), _node, EdgeType.Decides, Provenance.Model, 1.0, At: _at)]), Ct);
        await Commit(store, first);
        var model = new CostlyModel(new FakeModelPort { Answer = Extraction(Quote) }, 1_980);
        return (store, new DecisionPass(store, model, new LabelingSettings { Enabled = true, MaxTokensPerSync = 2_000 }, (new FakeScrubber(), new FakeTimeProvider(_at))), first);
    }

    [Fact]
    public async Task A_store_error_while_giving_up_the_pairs_stops_the_pass_with_its_code_and_keeps_the_record_queued()
    {
        var (store, pass, first) = await NoRoom();
        store.SaveConflictFailure = ContextErrors.StoreUnavailable.With("conflict save failed");

        await pass.Run([first], Ct);

        Assert.Equal(ContextErrors.StoreUnavailable.Code, pass.Failure);
        Assert.Contains(_big.Value, (await store.Queued(_linear, Ct)).Value.Select(k => k.Value), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData(1)]   // the conflict checks' read
    [InlineData(2)]   // the give-up's read
    public async Task A_failed_read_of_the_layer_in_the_conflict_checks_or_the_give_up_is_the_runs_failure(int call)
    {
        var (store, pass, first) = await NoRoom();
        store.LayerFailureOnCall = call;
        store.LayerFailure = ContextErrors.StoreUnavailable.With("layer read failed");

        await pass.Run([first], Ct);

        Assert.Equal(ContextErrors.StoreUnavailable.Code, pass.Failure);   // not read as "no pairs" (042) or as the cap (059)
        Assert.Contains(_big.Value, (await store.Queued(_linear, Ct)).Value.Select(k => k.Value), StringComparer.Ordinal);
    }
}
