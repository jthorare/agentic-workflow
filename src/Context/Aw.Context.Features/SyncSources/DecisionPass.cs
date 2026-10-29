using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Models;
using Aw.Context.Core.Trust;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// Decision extraction and conflict checks for the records one sync page committed (spec §6.1, §8.8, §8.9; S-5B). The model sees only the scrubbed,
/// fenced record text and the node keys its rules linked, and its answer is validated by <see cref="DecisionExtractor"/>. Everything it asks is counted
/// against 'maxTokensPerSync': a call that would go over the cap is not made, and the record stays queued for the next sync, until its conflict checks are through too. A record whose extraction prompt alone is over the cap can never be made, so it is refused (counted, no decisions, not queued) rather than held back forever; so is a record that, as the run's first model call, leaves no room under the cap for even one conflict check (its pairs are refused; one that got some pairs answered stays queued and continues next run).
/// </summary>
internal sealed class DecisionPass(IIntentStore store, IModelPort model, LabelingSettings labeling, (IIngestFilter Ingest, TimeProvider Clock) support)
{
    private const int ExtractAnswerTokens = 600;
    private const int VerdictAnswerTokens = 60;
    private const int VerdictTries = 3;
    private static readonly TimeSpan _askBudget = TimeSpan.FromSeconds(60);

    private bool _stopped;
    private bool _capped;
    private int _answered;   // pairs the run's conflict checks stored with a verdict: progress, so the cap isn't "can never fit"

    public int Tokens { get; private set; }

    public int Refused { get; private set; }

    public bool HeldBack { get; private set; }

    public string? Failure { get; private set; }

    public async ValueTask Run(IReadOnlyList<PendingRecord> pending, CancellationToken cancellationToken)
    {
        foreach (var record in pending)
        {
            var linked = record.Links.Select(l => l.Target).Where(k => k.Kind is not (NodeKind.Record or NodeKind.Decision)).Distinct().ToList();
            if (linked.Count == 0)
            {
                _ = await store.SaveDecisions(new DecisionBatch(record.Record.Key, record.Record.Version, [], []), cancellationToken).ConfigureAwait(false);   // nothing it could decide: not queued
            }
            else if (!labeling.Enabled || _stopped)
            {
                await HoldBack(record, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await Extract(record, linked, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask Extract(PendingRecord record, List<NodeKey> linked, CancellationToken cancellationToken)
    {
        var prompt = DecisionExtractor.Prompt(record.Record.Key, record.Text, linked);
        if (Cost(prompt, ExtractAnswerTokens) > labeling.MaxTokensPerSync)
        {
            await GiveUp(record, cancellationToken).ConfigureAwait(false);
            return;
        }

        var first = Tokens == 0;   // the run's first model call: nothing earlier in the run took any of the cap
        var asked = await Ask(ModelPurpose.DecisionExtraction, prompt, ExtractAnswerTokens, cancellationToken).ConfigureAwait(false);
        if (asked is null)
        {
            await HoldBack(record, cancellationToken).ConfigureAwait(false);
            return;
        }

        var read = DecisionExtractor.Read(asked, record.Record.Key, record.Text, linked, support.Clock.GetUtcNow());
        Refused += read.Refused;
        var decisions = read.Decisions.Select(d => d with { Wording = support.Ingest.Clean(d.Wording, singleLine: true) is { Blocked: false } clean ? clean.Text : d.Quote }).ToList();
        // The record stays queued until its conflict checks are through: a stopped check (the cap, the model down) leaves it to the next sync, which asks only the pairs not yet answered.
        var batch = new DecisionBatch(record.Record.Key, record.Record.Version, decisions, read.Edges);
        var saved = await store.SaveDecisions(decisions.Count == 0 ? batch : batch with { Pending = true }, cancellationToken).ConfigureAwait(false);
        if (saved.IsError)
        {
            Failure = saved.Error.Code;
            _stopped = true;
            return;
        }

        await Finish(batch, first, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The conflict checks of a saved pending batch; the batch is saved done once they are through (or given up).</summary>
    private async ValueTask Finish(DecisionBatch batch, bool first, CancellationToken cancellationToken)
    {
        var decisions = batch.Decisions;
        var finished = decisions.Count > 0 && await CheckConflicts(decisions, cancellationToken).ConfigureAwait(false);
        if (!finished && decisions.Count > 0 && first && _capped && _answered == 0 && Failure is null)
        {
            finished = await GiveUpPairs(decisions, cancellationToken).ConfigureAwait(false);   // its own extraction left no room for even one check, and a retry would again
        }

        if (finished)
        {
            var saved = await store.SaveDecisions(batch, cancellationToken).ConfigureAwait(false);
            if (saved.IsError)
            {
                Failure = saved.Error.Code;
                _stopped = true;
            }
        }
    }

    /// <summary>
    /// A record left unextracted this run waits for the next: a record already done (a map change re-fetched it) is queued again, keeping its old decisions until then,
    /// so the next run re-extracts it against the new map instead of skipping it as unchanged.
    /// </summary>
    private async ValueTask HoldBack(PendingRecord record, CancellationToken cancellationToken)
    {
        HeldBack = true;
        var queued = await store.Requeue(record.Record.Key, record.Record.Version, cancellationToken).ConfigureAwait(false);
        if (queued.IsError)
        {
            Failure = queued.Error.Code;
            _stopped = true;
        }
    }

    /// <summary>Asks about each unanswered pair the added decisions make; false when the checks stopped before the last pair or an answer could not be read yet.</summary>
    private async ValueTask<bool> CheckConflicts(IReadOnlyList<DecisionRow> added, CancellationToken cancellationToken)
    {
        if (await ReadLayer(cancellationToken).ConfigureAwait(false) is not { } layer)
        {
            return false;
        }

        var tried = layer.Conflicts.Where(c => c.Verdict == ConflictVerdict.Unread).ToDictionary(c => (c.A.Value, c.B.Value), c => c.Tries);
        var complete = true;
        foreach (var (first, second, _) in ConflictChecker.Pairs(layer, added))
        {
            var answer = await Ask(ModelPurpose.Adjudication, ConflictChecker.Prompt(first, second), VerdictAnswerTokens, cancellationToken).ConfigureAwait(false);
            if (answer is null)
            {
                return false;
            }

            var now = support.Clock.GetUtcNow();
            var pair = DecisionConflict.Of(first.ToKey(), second.ToKey(), ConflictChecker.Read(answer) ?? ConflictVerdict.Unread, now);
            if (pair.Verdict == ConflictVerdict.Unread)
            {
                pair = Unreadable(pair, tried.GetValueOrDefault((pair.A.Value, pair.B.Value)) + 1);
            }

            complete &= pair.Verdict != ConflictVerdict.Unread;

            var stored = await store.SaveConflict(pair, cancellationToken).ConfigureAwait(false);
            if (stored.IsError)
            {
                Failure = stored.Error.Code;
                _stopped = true;
                return false;
            }

            if (pair.Verdict != ConflictVerdict.Unread)
            {
                _answered++;
            }
        }

        return complete;
    }

    /// <summary>The stored layer; a failed read is the pass's failure, not "no pairs" (042) or the cap (059).</summary>
    private async ValueTask<IntentLayer?> ReadLayer(CancellationToken cancellationToken)
    {
        var layer = await store.Layer(cancellationToken).ConfigureAwait(false);
        if (layer.IsError)
        {
            Failure = layer.Error.Code;
            _stopped = true;
            return null;
        }

        return layer.Value;
    }

    /// <summary>An answer that can't be read leaves the pair unanswered and the record queued; the third unreadable answer gives the pair up (057), so it isn't asked forever.</summary>
    private DecisionConflict Unreadable(DecisionConflict pair, int tries)
    {
        Refused++;
        return pair with { Verdict = tries >= VerdictTries ? ConflictVerdict.Refused : ConflictVerdict.Unread, Tries = tries };
    }

    /// <summary>The record's prompt could never fit, in any run: counted refused, no decisions, not queued, instead of held back forever while it stops the pass for the records after it.</summary>
    private async ValueTask GiveUp(PendingRecord record, CancellationToken cancellationToken)
    {
        Refused++;
        var skipped = await store.SaveDecisions(new DecisionBatch(record.Record.Key, record.Record.Version, [], []), cancellationToken).ConfigureAwait(false);
        if (skipped.IsError)
        {
            Failure = skipped.Error.Code;
            _stopped = true;
        }
    }

    /// <summary>
    /// A record that was the run's first model call and could not fit even one conflict check under the cap never will (a retry re-extracts it first): its pairs are
    /// refused (counted, 057) so the record is done and the records after it get their turn. A record with a pair answered this run stays queued instead: the next run continues. False when a read or save failed.
    /// </summary>
    private async ValueTask<bool> GiveUpPairs(IReadOnlyList<DecisionRow> added, CancellationToken cancellationToken)
    {
        if (await ReadLayer(cancellationToken).ConfigureAwait(false) is not { } layer)
        {
            return false;
        }

        foreach (var (first, second, _) in ConflictChecker.Pairs(layer, added))
        {
            Refused++;
            var stored = await store.SaveConflict(DecisionConflict.Of(first.ToKey(), second.ToKey(), ConflictVerdict.Refused, support.Clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
            if (stored.IsError)
            {
                Failure = stored.Error.Code;
                _stopped = true;
                return false;
            }
        }

        return true;
    }

    private static bool IsHarnessDown(string code) =>
        string.Equals(code, ContextErrors.ModelHarnessesUnavailable.Code, StringComparison.Ordinal) || string.Equals(code, ContextErrors.NoModelHarness.Code, StringComparison.Ordinal);

    private static int Cost(string prompt, int answerTokens) => ((prompt.Length + 3) / 4) + answerTokens;

    private async ValueTask<string?> Ask(ModelPurpose purpose, string prompt, int answerTokens, CancellationToken cancellationToken)
    {
        if (_stopped || Tokens + Cost(prompt, answerTokens) > labeling.MaxTokensPerSync)
        {
            HeldBack = true;
            _capped |= !_stopped;   // the cap stopped it, not an earlier failure
            _stopped = true;
            return null;
        }

        var answer = await model.Ask(new ModelRequest(purpose, prompt, answerTokens, _askBudget), cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            _stopped = true;
            if (IsHarnessDown(answer.Error.Code))
            {
                HeldBack = true;   // every model harness is at its limit or none is installed (240, 241): the record stays queued for the next sync, which is no failure (model routing spec §5.4)
            }
            else
            {
                Failure = answer.Error.Code;
            }

            return null;
        }

        Tokens += answer.Value.InputTokens + answer.Value.OutputTokens;
        return answer.Value.Text;
    }
}
