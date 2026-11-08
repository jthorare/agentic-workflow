using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Core.Stack;

/// <summary>Decides whether the local database holds only seed data: a seed row exists and every profile email is on a seed domain.</summary>
public static class ProvenanceCheck
{
    /// <summary>Runs the check; any query failure is unknown, with the failure as the reason.</summary>
    /// <param name="query">The database.</param>
    /// <param name="policy">What counts as seed data.</param>
    /// <param name="database">The database to check (a worktree slot's), or null for the connection's default.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>The finding.</returns>
    public static async ValueTask<ProvenanceFinding> RunAsync(IProvenanceQuery query, SeedPolicy policy, string? database, CancellationToken cancellationToken)
    {
        var seeds = await query.EmailsLikeAsync(policy.EmailPattern, database, cancellationToken).ConfigureAwait(false);
        if (seeds.IsError)
        {
            return new(DbProvenance.Unknown, seeds.Error.Message);
        }

        if (seeds.Value.Count == 0)
        {
            return new(DbProvenance.Unknown, "no seed row found in the seed table");
        }

        var all = await query.AllEmailsAsync(database, cancellationToken).ConfigureAwait(false);
        return all.IsError ? new(DbProvenance.Unknown, all.Error.Message) : Decide(all.Value, policy);
    }

    private static ProvenanceFinding Decide(IReadOnlyList<string> emails, SeedPolicy policy)
    {
        var foreign = emails.FirstOrDefault(e => !policy.Domains.Contains(DomainOf(e), StringComparer.OrdinalIgnoreCase));
        return foreign is null
            ? new(DbProvenance.Seeded, "seed row present, every profile email domain is an allowed seed domain")
            : new(DbProvenance.Unknown, $"a profile row's email domain (\"{foreign}\") doesn't look like a seed — this data may be real");
    }

    private static string DomainOf(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at < 0 ? string.Empty : email[(at + 1)..];
    }
}
