namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The comment marks' SQL (ledger.db, migration 0007_pr_comment_marks).</summary>
internal static class PrCommentMarkSql
{
    internal const string Columns = "forge, repo, pr, comment_id, edited_at, instance, outcome, reason, \"commit\", at, effect";

    /// <summary>Appends the row; changes nothing when its effect identity is already recorded.</summary>
    internal const string Insert =
        "INSERT OR IGNORE INTO pr_comment_marks (forge, repo, pr, comment_id, edited_at, instance, outcome, reason, \"commit\", at, effect) " +
        "VALUES ($forge, $repo, $pr, $commentId, $editedAt, $instance, $outcome, $reason, $commit, $at, $effect)";

    /// <summary>The latest row per (comment, edited-at): the greatest 'at', the later write on a tie.</summary>
    internal const string SelectLatest =
        "SELECT " + Columns + " FROM pr_comment_marks m WHERE forge = $forge AND repo = $repo AND pr = $pr AND id = (" +
        "SELECT id FROM pr_comment_marks WHERE forge = m.forge AND repo = m.repo AND pr = m.pr AND comment_id = m.comment_id AND edited_at = m.edited_at " +
        "ORDER BY at DESC, id DESC LIMIT 1) ORDER BY id";

    internal const string Purge = "DELETE FROM pr_comment_marks WHERE forge = $forge AND repo = $repo AND pr = $pr";
}
