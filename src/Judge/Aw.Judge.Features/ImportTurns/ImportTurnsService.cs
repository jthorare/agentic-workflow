using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Briefs;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Transcripts;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ImportTurns;

// Rebuilds 'turn-progress' eval items from the Edit, Write and MultiEdit calls of transcripts, and labels them from what
// happened next in the same transcript (line survival, then the next real prompt). It reads and writes only eval items
// and labels, never decisions. A transcript is untrusted data: its text goes into the stored input only after the
// redactor (values and keys) and the cap, and it is never followed.
internal sealed class ImportTurnsService(
    IEvalStore items,
    ITranscriptReader transcripts,
    IBriefStore briefs,
    IInputRedactor redactor,
    IIdGenerator ids,
    TimeProvider clock,
    JsonSerializerOptions json) : IService<ImportTurnsRequest, ImportTurnsResponse>
{
    private const string Question = "turn-progress";
    private static readonly TimeSpan _sessionClosedAfter = TimeSpan.FromHours(6);

    public async ValueTask<Result<ImportTurnsResponse>> Handle(ImportTurnsRequest request, CancellationToken cancellationToken)
    {
        var total = new Totals();
        foreach (var file in request.Files)
        {
            var read = await transcripts.Read(file, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                total.Unreadable++;
                continue;
            }

            if (request.Since is { } since && clock.GetUtcNow() - read.Value.ModifiedAt > since)
            {
                continue;
            }

            // Outside the read: a store error surfaces as itself, not as an unreadable file.
            if (await Import(read.Value, total, cancellationToken).ConfigureAwait(false) is { IsError: true } failed)
            {
                return failed.Error;
            }
        }

        return new ImportTurnsResponse(total.Imported, total.Skipped, total.Unreadable);
    }

    private async ValueTask<Result<Unit>> Import(Transcript transcript, Totals total, CancellationToken cancellationToken)
    {
        var bySession = transcript.Turns.GroupBy(t => t.Session.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => (IReadOnlyList<TranscriptTurn>)[.. g], StringComparer.Ordinal);
        foreach (var turn in transcript.Turns)
        {
            if (turn.Edits.Count == 0)
            {
                total.Skipped++;
                continue;
            }

            var session = bySession[turn.Session.Value];
            var item = new EvalItem(
                EvalItemId.From(ids.NewId().ToString()),
                Question,
                InputCap.Write(redactor.Redact(await InputOf(turn, session, cancellationToken).ConfigureAwait(false)), InputCap.MaxChars, json),
                string.Create(CultureInfo.InvariantCulture, $"transcript:{turn.Session.Value}:{turn.Index}"),
                ModelDecision: null,
                clock.GetUtcNow());
            var added = await items.AddItem(item, cancellationToken).ConfigureAwait(false);
            if (added.IsError)
            {
                return added.Error;
            }

            if (!added.Value)
            {
                total.Skipped++;
                continue;
            }

            total.Imported++;
            var labeled = await Label(item, transcript, session, turn, cancellationToken).ConfigureAwait(false);
            if (labeled.IsError)
            {
                return labeled.Error;
            }
        }

        return Results.Ok();
    }

    // The outcome label from what happened next in the transcript; no label yet is not an error.
    private async ValueTask<Result<Unit>> Label(EvalItem item, Transcript transcript, IReadOnlyList<TranscriptTurn> session, TranscriptTurn turn, CancellationToken cancellationToken)
    {
        var closed = transcript.SessionEnds.TryGetValue(turn.Session, out var end) && item.CreatedAt - end >= _sessionClosedAfter;
        return TurnOutcomes.TurnLabel(session, turn.Index, closed) is { } label
            ? await items.RecordLabel(item.Id, LabelSource.Outcome, label, item.CreatedAt, cancellationToken).ConfigureAwait(false)
            : Results.Ok();
    }

    // The turn-progress input, camel-cased as the question reads it. The brief of a task session (if the scope gate saved
    // one) gives the goal and the criteria; a brief store that can't be read only costs that enrichment.
    private async ValueTask<JsonNode> InputOf(TranscriptTurn turn, IReadOnlyList<TranscriptTurn> session, CancellationToken cancellationToken)
    {
        Brief? brief = null;
        if (ToolUseId.TryFrom($"task:{turn.Session.Value}", out var toolUse))
        {
            var found = await briefs.ByToolUseId(toolUse, cancellationToken).ConfigureAwait(false);
            brief = found.IsSuccess ? found.Value : null;
        }

        var earlier = session.Take(turn.Index).SelectMany(t => t.Edits.Select(e => e.File)).Distinct(StringComparer.Ordinal).Count();
        return new JsonObject
        {
            ["problem"] = brief is null ? turn.Prompt : $"Goal: {brief.Goal}\n\n{turn.Prompt}",
            ["acceptanceCriteria"] = brief?.AcceptanceCriteria ?? "",
            ["turnDiff"] = TurnOutcomes.TurnDiff(turn.Edits),
            ["priorDiffStat"] = string.Create(CultureInfo.InvariantCulture, $"{earlier} files edited earlier in session"),
            ["signals"] = "",
        };
    }

    private sealed class Totals
    {
        public int Imported { get; set; }

        public int Skipped { get; set; }

        public int Unreadable { get; set; }
    }
}
