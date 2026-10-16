namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The item claims' SQL (ledger.db, migration 0006_workflow_claims).</summary>
internal static class ClaimSql
{
    internal const string SelectClaim = "SELECT item, owner, attempt FROM workflow_claims WHERE item = $item";

    internal const string InsertClaim =
        "INSERT INTO workflow_claims (item, owner, attempt, claimed_at) VALUES ($item, $owner, $attempt, $claimedAt)";

    /// <summary>Takes the claim over from the holder the caller found ended; changes nothing when another instance holds it by now.</summary>
    internal const string ReplaceEndedClaim =
        "UPDATE workflow_claims SET owner = $owner, attempt = $attempt, claimed_at = $claimedAt WHERE item = $item AND owner = $ended";
}
