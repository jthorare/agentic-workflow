using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Core.Stack;

/// <summary>
/// Decides whether the local database was seeded: a profile row matches the seed pattern. The other rows' emails don't
/// matter (seeds use faker addresses on real-looking domains); the host guard already keeps runs off prod, and a prod dump
/// restored locally has no seed row.
/// </summary>
public static class ProvenanceCheck
{
    /// <summary>Runs the check; any query failure is unknown, with the failure as the reason.</summary>
    /// <param name="query">The database.</param>
    /// <param name="policy">What counts as seed data.</param>
    /// <param name="database">The database to check (a worktree slot's), or null for the connection's default.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The finding.</returns>
    public static async ValueTask<ProvenanceFinding> RunAsync(IProvenanceQuery query, SeedPolicy policy, string? database, CancellationToken cancellationToken)
    {
        var seeds = await query.EmailsLikeAsync(policy.EmailPattern, database, cancellationToken).ConfigureAwait(false);
        if (seeds.IsError)
        {
            return new(DbProvenance.Unknown, seeds.Error.Message);
        }

        return seeds.Value.Count == 0
            ? new(DbProvenance.Unknown, "no seed row found in the seed table")
            : new(DbProvenance.Seeded, $"seed row present (a profile email matches {policy.EmailPattern})");
    }
}
