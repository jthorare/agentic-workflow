using System.Globalization;
using System.Text;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Kernel.Data;

namespace Aw.Judge.Infrastructure.Eval;

/// <summary>The eval store's SQL: named constants and the statements that bind them.</summary>
internal static class EvalSql
{
    // The item's columns, in the order EvalMappers reads them.
    internal const string ItemColumns = "e.id, e.question, e.input_json, e.source, e.model_decision, e.created_at";

    internal const string InsertItem =
        "INSERT OR IGNORE INTO eval_items (id, question, input_json, source, model_decision, created_at) " +
        "VALUES ($id, $question, $input_json, $source, $model_decision, $created_at)";

    internal const string FindItem = "SELECT " + ItemColumns + " FROM eval_items e WHERE e.id = $id";

    internal const string ItemExists = "SELECT 1 FROM eval_items WHERE id = $id";

    // One label per item and source: a second label from the same source replaces the first.
    internal const string UpsertLabel =
        "INSERT OR REPLACE INTO labels (item_id, source, label, labeled_at) VALUES ($item_id, $source, $label, $labeled_at)";

    internal const string Labeled =
        "SELECT " + ItemColumns + ", l.label, l.source FROM eval_items e JOIN labels l ON l.item_id = e.id " +
        "WHERE e.question = $question AND l.label != 'skip' ORDER BY e.created_at ASC, e.id ASC";

    internal const string LabeledFrom =
        "SELECT " + ItemColumns + ", l.label, l.source FROM eval_items e JOIN labels l ON l.item_id = e.id " +
        "WHERE e.question = $question AND l.label != 'skip' AND l.source = $source ORDER BY e.created_at ASC, e.id ASC";

    internal const string Counts =
        "SELECT e.question, COUNT(*), " +
        "SUM(CASE WHEN EXISTS (SELECT 1 FROM labels l WHERE l.item_id = e.id AND l.label != 'skip') THEN 1 ELSE 0 END), " +
        "SUM(CASE WHEN NOT EXISTS (SELECT 1 FROM labels l WHERE l.item_id = e.id AND l.label != 'skip') " +
        "AND EXISTS (SELECT 1 FROM labels l WHERE l.item_id = e.id AND l.label = 'skip') THEN 1 ELSE 0 END) " +
        "FROM eval_items e GROUP BY e.question ORDER BY e.question";

    internal const string Agreement =
        "SELECT COUNT(*), SUM(CASE WHEN o.label = a.label THEN 1 ELSE 0 END) FROM eval_items e " +
        "JOIN labels o ON o.item_id = e.id AND o.source = 'outcome' AND o.label != 'skip' " +
        "JOIN labels a ON a.item_id = e.id AND a.source = 'adjudicator' AND a.label != 'skip' WHERE e.question = $question";

    // The first word and the optional question filter; the statement is the same text either way except the last clause.
    internal const string DecidedSince =
        "SELECT d.id, d.question, d.decision, x.input_json FROM decisions d JOIN decision_details x ON x.id = d.id " +
        "WHERE d.outcome = 'decided' AND d.ts >= $since";

    internal const string DecidedOrder = " ORDER BY d.ts ASC, d.id ASC";

    internal const string OutcomeCandidates =
        "SELECT e.id, d.ts, d.decision, x.session_id FROM eval_items e " +
        "JOIN decisions d ON e.source = 'decision:' || d.id JOIN decision_details x ON x.id = d.id " +
        "LEFT JOIN labels l ON l.item_id = e.id AND l.source = 'outcome' " +
        "WHERE e.question = 'ask-check' AND l.item_id IS NULL ORDER BY d.ts ASC, e.id ASC";

    internal static SqlStatement Insert(EvalItem item, string inputJson) => new(InsertItem, c => c
        .Bind("$id", item.Id.Value).Bind("$question", item.Question).Bind("$input_json", inputJson).Bind("$source", item.Source)
        .Bind("$model_decision", item.ModelDecision).Bind("$created_at", item.CreatedAt), "eval item " + item.Id.Value);

    internal static SqlStatement Find(EvalItemId id) => new(FindItem, c => c.Bind("$id", id.Value), "eval item " + id.Value);

    internal static SqlStatement Exists(EvalItemId id) => new(ItemExists, c => c.Bind("$id", id.Value), "eval item " + id.Value);

    internal static SqlStatement Label(EvalItemId item, LabelSource source, string label, DateTimeOffset at) => new(UpsertLabel, c => c
        .Bind("$item_id", item.Value).Bind("$source", LabelSources.Name(source)).Bind("$label", label).Bind("$labeled_at", at),
        "label of " + item.Value);

    internal static SqlStatement LabeledOf(string question, LabelSource? source) => source is { } from
        ? new(LabeledFrom, c => c.Bind("$question", question).Bind("$source", LabelSources.Name(from)))
        : new(Labeled, c => c.Bind("$question", question));

    internal static SqlStatement AgreementOf(string question) => new(Agreement, c => c.Bind("$question", question));

    internal static SqlStatement Decided(string? question, DateTimeOffset since) => new(
        DecidedSince + (question is null ? "" : " AND d.question = $question") + DecidedOrder,
        c =>
        {
            _ = c.Bind("$since", since);
            if (question is not null)
            {
                _ = c.Bind("$question", question);
            }
        });

    // Unlabeled by one source (a label from another doesn't count) or by every source; one filter built in one place.
    internal static SqlStatement Next(string? question, LabelSource? source, IReadOnlySet<EvalItemId> exclude)
    {
        var sql = new StringBuilder("SELECT ").Append(ItemColumns).Append(" FROM eval_items e LEFT JOIN labels l ON l.item_id = e.id");
        if (source is not null)
        {
            _ = sql.Append(" AND l.source = $source");
        }

        _ = sql.Append(" WHERE l.item_id IS NULL");
        if (question is not null)
        {
            _ = sql.Append(" AND e.question = $question");
        }

        var skipped = exclude.Select(static i => i.Value).Order(StringComparer.Ordinal).ToList();
        if (skipped.Count > 0)
        {
            _ = sql.Append(" AND e.id NOT IN (").AppendJoin(", ", skipped.Select(static (_, i) => Exclude(i))).Append(')');
        }

        _ = sql.Append(" ORDER BY e.created_at ASC, e.id ASC LIMIT 1");
        return new(sql.ToString(), c =>
        {
            if (source is { } from)
            {
                _ = c.Bind("$source", LabelSources.Name(from));
            }

            if (question is not null)
            {
                _ = c.Bind("$question", question);
            }

            for (var i = 0; i < skipped.Count; i++)
            {
                _ = c.Bind(Exclude(i), skipped[i]);
            }
        });
    }

    private static string Exclude(int index) => "$exclude" + index.ToString(CultureInfo.InvariantCulture);
}
