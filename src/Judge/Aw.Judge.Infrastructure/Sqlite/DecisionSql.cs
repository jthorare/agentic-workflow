using System.Globalization;
using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ids;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>The decision log's SQL: named constants and the statements that bind them.</summary>
internal static class DecisionSql
{
    internal const string InsertDecision =
        "INSERT INTO decisions (id, ts, question, content_class, provider, decision, confidence, reason_code, latency_ms, " +
        "input_digest, undone_at, chain_position, skipped, outcome, approved_at) VALUES ($id, $ts, $question, $content_class, " +
        "$provider, $decision, $confidence, $reason_code, $latency_ms, $input_digest, $undone_at, $chain_position, $skipped, " +
        "$outcome, $approved_at)";

    internal const string InsertDetails =
        "INSERT INTO decision_details (id, input_json, probabilities, rules_opinion, agreement, session_id, reasons) " +
        "VALUES ($id, $input_json, $probabilities, $rules_opinion, $agreement, $session_id, $reasons)";

    internal const string InsertFailure =
        "INSERT INTO failures (ts, question, provider, reason_code) VALUES ($ts, $question, $provider, $reason_code)";

    internal const string Find =
        "SELECT d.id, d.ts, d.question, d.content_class, d.provider, d.decision, d.confidence, d.reason_code, d.latency_ms, " +
        "d.input_digest, d.undone_at, d.chain_position, d.skipped, d.outcome, d.approved_at, " +
        "x.id, x.input_json, x.probabilities, x.rules_opinion, x.agreement, x.session_id, x.reasons " +
        "FROM decisions d LEFT JOIN decision_details x ON x.id = d.id WHERE d.id = $id";

    internal const string MarkUndone = "UPDATE decisions SET undone_at = $at WHERE id = $id";

    // Approving twice keeps the first time; the row still counts as changed, so a known id is never 'unknown'.
    internal const string MarkApproved = "UPDATE decisions SET approved_at = COALESCE(approved_at, $at) WHERE id = $id";

    internal const string CountFailuresSince = "SELECT COUNT(*) FROM failures WHERE ts >= $since";

    // "Model attempts" leave out rows that never reached a provider: Core's EvaluationReasons.BeforeChain (a disabled
    // question, invalid input, a pre-rule settlement, a rule timeout), bound as $before0..n. A fallback after an unsure
    // model did reach one, so it counts.
    internal static string LastAttemptOutcomes { get; } =
        "SELECT outcome FROM decisions WHERE outcome IN ('decided', 'failed') AND reason_code NOT IN (" +
        string.Join(", ", EvaluationReasons.BeforeChain.Select((_, i) => BeforeChainParameter(i))) + ") " +
        "ORDER BY ts DESC, rowid DESC LIMIT $count";

    // Read first: with nothing to drop (every question but the day's first) the prune never takes the write lock.
    // CROSS JOIN keeps the order as written: scan decision_details (only the retention window) and look each decision
    // up by id, so the cost doesn't grow with the decision history, which is never deleted.
    internal const string HasDetailsBefore =
        "SELECT COUNT(*) FROM (SELECT 1 FROM decision_details x CROSS JOIN decisions d ON d.id = x.id WHERE d.ts < $before LIMIT 1)";

    internal const string PruneDetailsBefore =
        "DELETE FROM decision_details WHERE id IN (SELECT id FROM decisions WHERE ts < $before)";

    internal static SqlStatement Insert(DecisionRecord d, DecisionJson json) => new(InsertDecision, c => c
        .Bind("$id", d.Id.Value).Bind("$ts", d.Ts).Bind("$question", d.Question).Bind("$content_class", d.ContentClass)
        .Bind("$provider", d.Provider).Bind("$decision", d.Decision).Bind("$confidence", d.Confidence)
        .Bind("$reason_code", d.ReasonCode).Bind("$latency_ms", d.LatencyMs).Bind("$input_digest", d.InputDigest)
        .BindNullable("$undone_at", d.UndoneAt).Bind("$chain_position", d.ChainPosition)
        .Bind("$skipped", json.Skipped(d.Skipped)).Bind("$outcome", DecisionText.Of(d.Outcome))
        .BindNullable("$approved_at", d.ApprovedAt), "decision " + d.Id.Value);

    internal static SqlStatement Insert(DecisionId id, DecisionDetails x, DecisionJson json) => new(InsertDetails, c => c
        .Bind("$id", id.Value).Bind("$input_json", x.InputJson).Bind("$probabilities", json.Probabilities(x.Probabilities))
        .Bind("$rules_opinion", x.RulesOpinion).Bind("$agreement", x.Agreement is { } a ? DecisionText.Of(a) : null)
        .Bind("$session_id", x.SessionId?.Value).Bind("$reasons", json.Reasons(x.Reasons)), "decision details " + id.Value);

    internal static SqlStatement Insert(FailureRecord f) => new(InsertFailure, c => c
        .Bind("$ts", f.Ts).Bind("$question", f.Question).Bind("$provider", f.Provider).Bind("$reason_code", f.ReasonCode));

    internal static SqlStatement ById(string sql, DecisionId id) => new(sql, c => c.Bind("$id", id.Value), "decision " + id.Value);

    internal static SqlStatement Undo(DecisionId id, DateTimeOffset at) =>
        new(MarkUndone, c => c.Bind("$id", id.Value).Bind("$at", at), "decision " + id.Value);

    internal static SqlStatement Approve(DecisionId id, DateTimeOffset at) =>
        new(MarkApproved, c => c.Bind("$id", id.Value).Bind("$at", at), "decision " + id.Value);

    internal static SqlStatement At(string sql, string name, DateTimeOffset at) => new(sql, c => c.Bind(name, at));

    internal static SqlStatement Last(int count) => new(LastAttemptOutcomes, c =>
    {
        _ = c.Bind("$count", count);
        for (var i = 0; i < EvaluationReasons.BeforeChain.Count; i++)
        {
            _ = c.Bind(BeforeChainParameter(i), EvaluationReasons.BeforeChain[i]);
        }
    });

    private static string BeforeChainParameter(int index) => "$before" + index.ToString(CultureInfo.InvariantCulture);

    // SqliteBind has no nullable DateTimeOffset overload: bind the time through it, or a typed null.
    private static SqliteCommand BindNullable(this SqliteCommand command, string name, DateTimeOffset? at) =>
        at is { } value ? command.Bind(name, value) : command.Bind(name, (string?)null);
}
