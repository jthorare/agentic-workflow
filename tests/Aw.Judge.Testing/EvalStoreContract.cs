using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>
/// The IEvalStore contract: the same tests run against the in-memory fake and every real adapter. A derived class supplies
/// a fresh, empty store, one that can't be used, and the way a decided decision with details gets into the store's reach
/// (the real adapter reads the decision log; the fake is seeded).
/// </summary>
public abstract class EvalStoreContract
{
    private static readonly DateTimeOffset _t0 = KnownInstants.Epoch;

    /// <summary>A fresh, empty store.</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IEvalStore> Create(CancellationToken cancellationToken);

    /// <summary>A store that can't be opened.</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IEvalStore> CreateUnavailable(CancellationToken cancellationToken);

    /// <summary>Records a decided decision with details so the store's decision reads see it.</summary>
    /// <param name="store">The store from <see cref="Create"/>.</param>
    /// <param name="input">The decision and its stored input.</param>
    /// <param name="at">When it was decided.</param>
    /// <param name="session">The session its details kept, if any.</param>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>A task.</returns>
    protected abstract ValueTask SeedDecision(IEvalStore store, DecidedInput input, DateTimeOffset at, SessionId? session, CancellationToken cancellationToken);

    private static EvalItem Item(string id, string question = "ask-check", int minute = 0, string? source = null) =>
        new(EvalItemId.From(id), question, """{"transcriptTail":"x"}""", source ?? ("decision:" + id), "continue", _t0.AddMinutes(minute));

    private static EvalItemId Id(string id) => EvalItemId.From(id);

    private static DecidedInput Decided(string id, string question, string answer, string tail) =>
        new(DecisionId.From(id), question, answer, "{\"transcriptTail\":\"" + tail + "\"}");

    /// <summary>An item is added once per source.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_item_is_added_once_per_source()
    {
        var store = await Create(CancellationToken.None);

        var first = await store.AddItem(Item("item-1"), CancellationToken.None);
        var again = await store.AddItem(Item("item-2", source: "decision:item-1"), CancellationToken.None);

        Expect.That(first.IsSuccess && first.Value, "the first import adds the item");
        Expect.That(again.IsSuccess && !again.Value, "the same source is not imported twice");
        var found = await store.FindItem(Id("item-1"), CancellationToken.None);
        Expect.That(found.IsSuccess && string.Equals(found.Value.Question, "ask-check", StringComparison.Ordinal) && found.Value.CreatedAt == _t0, "the item reads back");
        Expect.That((await store.FindItem(Id("item-2"), CancellationToken.None)).IsError, "the refused duplicate was not stored");
    }

    /// <summary>An item nobody added is AW-JUDGE-014.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_unknown_item_is_AW_JUDGE_013()
    {
        var store = await Create(CancellationToken.None);

        var found = await store.FindItem(Id("nope"), CancellationToken.None);
        var labelled = await store.RecordLabel(Id("nope"), LabelSource.Override, "ask", _t0, CancellationToken.None);

        Expect.That(found.IsError && found.Error.Code == JudgeErrors.UnknownItem.Code, "FindItem says AW-JUDGE-014");
        Expect.That(labelled.IsError && labelled.Error.Code == JudgeErrors.UnknownItem.Code, "RecordLabel says AW-JUDGE-014");
    }

    /// <summary>A secret in an input's values or keys never reaches the store, and what is stored stays JSON.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_secret_in_an_input_never_reaches_the_store()
    {
        var store = await Create(CancellationToken.None);
        var secret = SampleSecrets.AnthropicKey;
        var input = "{\"transcriptTail\":\"deploy with " + secret + " now\",\"" + secret + "\":\"as a key\"}";

        _ = await store.AddItem(Item("item-1") with { InputJson = input }, CancellationToken.None);

        var found = await store.FindItem(Id("item-1"), CancellationToken.None);
        Expect.That(found.IsSuccess, "the item is found");
        Expect.That(!found.Value.InputJson.Contains(secret, StringComparison.Ordinal), "the secret is gone from values and keys");
        Expect.That(found.Value.InputJson.Contains("transcriptTail", StringComparison.Ordinal), "the rest of the input stays");
    }

    /// <summary>A source has one label per item; a second replaces it and the other sources' labels stay.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_second_label_from_a_source_replaces_the_first()
    {
        var store = await Create(CancellationToken.None);
        _ = await store.AddItem(Item("item-1"), CancellationToken.None);

        _ = await store.RecordLabel(Id("item-1"), LabelSource.Outcome, "continue", _t0, CancellationToken.None);
        _ = await store.RecordLabel(Id("item-1"), LabelSource.Adjudicator, "ask", _t0, CancellationToken.None);
        _ = await store.RecordLabel(Id("item-1"), LabelSource.Outcome, "ask", _t0.AddMinutes(1), CancellationToken.None);

        var outcome = await store.Labeled("ask-check", LabelSource.Outcome, CancellationToken.None);
        var adjudicator = await store.Labeled("ask-check", LabelSource.Adjudicator, CancellationToken.None);
        Expect.That(outcome.IsSuccess && outcome.Value.Count == 1 && string.Equals(outcome.Value[0].Label, "ask", StringComparison.Ordinal), "the outcome label was replaced");
        Expect.That(adjudicator.IsSuccess && adjudicator.Value.Count == 1 && string.Equals(adjudicator.Value[0].Label, "ask", StringComparison.Ordinal), "the adjudicator label stayed");
    }

    /// <summary>The next unlabeled item is the oldest one, filtered by question, source and exclusion.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_next_unlabeled_item_is_the_oldest_that_passes_the_filters()
    {
        var store = await Create(CancellationToken.None);
        _ = await store.AddItem(Item("item-b", minute: 2), CancellationToken.None);
        _ = await store.AddItem(Item("item-a", minute: 1), CancellationToken.None);
        _ = await store.AddItem(Item("item-w", question: "wake-gate", minute: 0), CancellationToken.None);
        _ = await store.RecordLabel(Id("item-a"), LabelSource.Adjudicator, "ask", _t0, CancellationToken.None);

        var any = await store.NextUnlabeled(question: null, source: null, new HashSet<EvalItemId>(), CancellationToken.None);
        var ask = await store.NextUnlabeled("ask-check", source: null, new HashSet<EvalItemId>(), CancellationToken.None);
        var byOutcome = await store.NextUnlabeled("ask-check", LabelSource.Outcome, new HashSet<EvalItemId>(), CancellationToken.None);
        var excluded = await store.NextUnlabeled("ask-check", LabelSource.Outcome, new HashSet<EvalItemId> { Id("item-a") }, CancellationToken.None);
        var none = await store.NextUnlabeled("ask-check", LabelSource.Outcome, new HashSet<EvalItemId> { Id("item-a"), Id("item-b") }, CancellationToken.None);

        Expect.That(any.IsSuccess && any.Value is { } w && string.Equals(w.Id.Value, "item-w", StringComparison.Ordinal), "every question: the oldest overall");
        Expect.That(ask.IsSuccess && ask.Value is { } b && string.Equals(b.Id.Value, "item-b", StringComparison.Ordinal), "unlabeled by every source skips an item any source labelled");
        Expect.That(byOutcome.IsSuccess && byOutcome.Value is { } a && string.Equals(a.Id.Value, "item-a", StringComparison.Ordinal), "another source's label does not count");
        Expect.That(excluded.IsSuccess && excluded.Value is { } e && string.Equals(e.Id.Value, "item-b", StringComparison.Ordinal), "an excluded item is passed over");
        Expect.That(none.IsSuccess && none.Value is null, "null when none is left");
    }

    /// <summary>Labeled items carry their strongest label, never a skip, oldest first.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Labeled_items_carry_their_strongest_label_and_never_a_skip()
    {
        var store = await Create(CancellationToken.None);
        _ = await store.AddItem(Item("item-2", minute: 2), CancellationToken.None);
        _ = await store.AddItem(Item("item-1", minute: 1), CancellationToken.None);
        _ = await store.AddItem(Item("item-3", minute: 3), CancellationToken.None);
        _ = await store.RecordLabel(Id("item-1"), LabelSource.Adjudicator, "ask", _t0, CancellationToken.None);
        _ = await store.RecordLabel(Id("item-1"), LabelSource.Outcome, "continue", _t0, CancellationToken.None);
        _ = await store.RecordLabel(Id("item-1"), LabelSource.Override, "ask", _t0, CancellationToken.None);
        _ = await store.RecordLabel(Id("item-2"), LabelSource.Adjudicator, "skip", _t0, CancellationToken.None);
        _ = await store.RecordLabel(Id("item-3"), LabelSource.Adjudicator, "continue", _t0, CancellationToken.None);
        _ = await store.RecordLabel(Id("item-3"), LabelSource.Outcome, "ask", _t0, CancellationToken.None);

        var labeled = await store.Labeled("ask-check", source: null, CancellationToken.None);

        Expect.That(labeled.IsSuccess && labeled.Value.Count == 2, "the skipped item is left out");
        Expect.That(labeled.Value[0].Item.Id.Value == "item-1" && labeled.Value[0].Source == LabelSource.Override && labeled.Value[0].Label == "ask", "an override beats the other sources");
        Expect.That(labeled.Value[1].Item.Id.Value == "item-3" && labeled.Value[1].Source == LabelSource.Outcome && labeled.Value[1].Label == "ask", "an outcome beats an adjudicator");
    }

    /// <summary>Counts cover every question; agreement counts items with both an outcome and an adjudicator label.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Counts_and_agreement_summarise_the_labels()
    {
        var store = await Create(CancellationToken.None);
        foreach (var n in new[] { "item-1", "item-2", "item-3", "item-4" })
        {
            _ = await store.AddItem(Item(n), CancellationToken.None);
        }

        _ = await store.AddItem(Item("item-w", question: "wake-gate"), CancellationToken.None);
        _ = await store.RecordLabel(Id("item-1"), LabelSource.Outcome, "ask", _t0, CancellationToken.None);
        _ = await store.RecordLabel(Id("item-1"), LabelSource.Adjudicator, "ask", _t0, CancellationToken.None);
        _ = await store.RecordLabel(Id("item-2"), LabelSource.Outcome, "ask", _t0, CancellationToken.None);
        _ = await store.RecordLabel(Id("item-2"), LabelSource.Adjudicator, "continue", _t0, CancellationToken.None);
        _ = await store.RecordLabel(Id("item-3"), LabelSource.Override, "skip", _t0, CancellationToken.None);

        var counts = await store.Counts(CancellationToken.None);
        var agreement = await store.Agreement("ask-check", CancellationToken.None);
        var empty = await store.Agreement("wake-gate", CancellationToken.None);

        Expect.That(counts.IsSuccess && counts.Value.Select(c => c.Question).SequenceEqual(["ask-check", "wake-gate"], StringComparer.Ordinal), "one count per question, by name");
        Expect.That(counts.Value[0] == new LabelCount("ask-check", 4, 2, 1), "ask-check: four items, two labelled, one skipped");
        Expect.That(counts.Value[1] == new LabelCount("wake-gate", 1, 0, 0), "wake-gate: one unlabelled item");
        Expect.That(agreement.IsSuccess && agreement.Value == new LabelAgreement(2, 1), "two shared items, one agreed");
        Expect.That(empty.IsSuccess && empty.Value == new LabelAgreement(0, 0), "no shared items counts zero, not an error");
    }

    /// <summary>The decided decisions in the window come oldest first, for the question asked.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Decided_decisions_come_from_the_window_oldest_first()
    {
        var store = await Create(CancellationToken.None);
        await SeedDecision(store, Decided("d-late", "ask-check", "ask", "b"), _t0.AddDays(2), session: null, CancellationToken.None);
        await SeedDecision(store, Decided("d-early", "ask-check", "continue", "a"), _t0.AddDays(1), session: null, CancellationToken.None);
        await SeedDecision(store, Decided("d-old", "ask-check", "ask", "c"), _t0, session: null, CancellationToken.None);
        await SeedDecision(store, Decided("d-wake", "wake-gate", "wake", "m"), _t0.AddDays(1), session: null, CancellationToken.None);

        var all = await store.DecidedSince(question: null, _t0.AddDays(1), CancellationToken.None);
        var asks = await store.DecidedSince("ask-check", _t0.AddDays(1), CancellationToken.None);

        Expect.That(all.IsSuccess && all.Value.Select(d => d.Id.Value).SequenceEqual(["d-early", "d-wake", "d-late"], StringComparer.Ordinal), "the window's decisions, oldest first");
        Expect.That(asks.IsSuccess && asks.Value.Select(d => d.Id.Value).SequenceEqual(["d-early", "d-late"], StringComparer.Ordinal), "only the question asked");
        Expect.That(all.Value[0] == Decided("d-early", "ask-check", "continue", "a"), "the answer and input come back");
    }

    /// <summary>The outcome candidates are the ask-check items made from a decision that have no outcome label yet.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Outcome_candidates_are_unlabeled_ask_check_items_made_from_decisions()
    {
        var store = await Create(CancellationToken.None);
        await SeedDecision(store, Decided("d1", "ask-check", "ask", "a"), _t0.AddMinutes(5), SessionId.From("s1"), CancellationToken.None);
        await SeedDecision(store, Decided("d2", "ask-check", "continue", "b"), _t0.AddMinutes(1), session: null, CancellationToken.None);
        await SeedDecision(store, Decided("d3", "ask-check", "ask", "c"), _t0.AddMinutes(2), session: null, CancellationToken.None);
        foreach (var n in new[] { "d1", "d2", "d3" })
        {
            _ = await store.AddItem(Item("item-" + n, source: "decision:" + n), CancellationToken.None);
        }

        _ = await store.AddItem(Item("item-x", source: "transcript:s:1"), CancellationToken.None);
        _ = await store.RecordLabel(Id("item-d3"), LabelSource.Outcome, "ask", _t0, CancellationToken.None);
        _ = await store.RecordLabel(Id("item-d2"), LabelSource.Adjudicator, "ask", _t0, CancellationToken.None);

        var candidates = await store.OutcomeCandidates(CancellationToken.None);

        Expect.That(candidates.IsSuccess && candidates.Value.Count == 2, "an outcome-labelled item and a transcript item are left out");
        Expect.That(candidates.Value[0] == new OutcomeCandidate(Id("item-d2"), _t0.AddMinutes(1), "continue", Session: null), "oldest decision first; another source's label does not count");
        Expect.That(candidates.Value[1] == new OutcomeCandidate(Id("item-d1"), _t0.AddMinutes(5), "ask", SessionId.From("s1")), "the session of the details comes back");
    }

    /// <summary>A store that can't be opened answers every call with an error and throws nothing.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_unusable_store_answers_with_errors()
    {
        var store = await CreateUnavailable(CancellationToken.None);
        var ct = CancellationToken.None;

        Expect.That((await store.AddItem(Item("item-1"), ct)).IsError, "AddItem is an error");
        Expect.That((await store.FindItem(Id("item-1"), ct)).IsError, "FindItem is an error");
        Expect.That((await store.RecordLabel(Id("item-1"), LabelSource.Override, "ask", _t0, ct)).IsError, "RecordLabel is an error");
        Expect.That((await store.NextUnlabeled(question: null, source: null, new HashSet<EvalItemId>(), ct)).IsError, "NextUnlabeled is an error");
        Expect.That((await store.Labeled("ask-check", source: null, ct)).IsError, "Labeled is an error");
        Expect.That((await store.Counts(ct)).IsError, "Counts is an error");
        Expect.That((await store.Agreement("ask-check", ct)).IsError, "Agreement is an error");
        Expect.That((await store.DecidedSince(question: null, _t0, ct)).IsError, "DecidedSince is an error");
        Expect.That((await store.OutcomeCandidates(ct)).IsError, "OutcomeCandidates is an error");
    }
}
