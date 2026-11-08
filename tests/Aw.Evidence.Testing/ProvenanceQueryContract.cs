using Aw.Evidence.Core;
using Aw.Evidence.Core.Stack;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>
/// The IProvenanceQuery contract: LIKE returns only the matching profile emails, every email comes back from the
/// unfiltered query, and a failed query is AW-EVIDENCE-014 carrying the database's message.
/// </summary>
public abstract class ProvenanceQueryContract
{
    /// <summary>Creates a query over a profile table holding <paramref name="emails"/>.</summary>
    /// <param name="emails">The profile rows' emails, in order.</param>
    /// <returns>The query.</returns>
    protected abstract IProvenanceQuery Create(IReadOnlyList<string> emails);

    /// <summary>Creates a query whose database fails with <paramref name="message"/>.</summary>
    /// <param name="message">The database's error.</param>
    /// <returns>The query.</returns>
    protected abstract IProvenanceQuery CreateFailing(string message);

    /// <summary>The seed pattern returns only matching emails.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_seed_pattern_returns_only_matching_emails()
    {
        var query = Create(["seed-admin-1@example.com", "staff@example.com", "seed-admin-2@example.com"]);

        var seeds = await query.EmailsLikeAsync("seed-admin-%@example.com", database: null, CancellationToken.None);

        Expect.That(seeds.Value.SequenceEqual(["seed-admin-1@example.com", "seed-admin-2@example.com"], StringComparer.Ordinal), "only the matching rows, in order");
    }

    /// <summary>Every email comes back from the unfiltered query.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Every_email_comes_back()
    {
        var query = Create(["seed-admin-1@example.com", "real.person@gmail.com"]);

        var all = await query.AllEmailsAsync(database: null, CancellationToken.None);

        Expect.That(all.Value.SequenceEqual(["seed-admin-1@example.com", "real.person@gmail.com"], StringComparer.Ordinal), "every row");
    }

    /// <summary>A profile row with no email (NULL) comes back as an empty email, never dropped: it can't pass as a seed.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_row_without_an_email_comes_back_empty_and_is_never_dropped()
    {
        var query = Create(["seed-admin-1@example.com", string.Empty]);

        var all = await query.AllEmailsAsync(database: null, CancellationToken.None);

        Expect.That(all.Value.SequenceEqual(["seed-admin-1@example.com", string.Empty], StringComparer.Ordinal), "the row without an email is kept, as empty");
    }

    /// <summary>A failed query is a probe failure with the database's message.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_failed_query_is_a_probe_failure_with_the_message()
    {
        var all = await CreateFailing("relation \"public.users\" does not exist").AllEmailsAsync(database: null, CancellationToken.None);

        Expect.That(all.IsError && all.Error.Code == EvidenceErrors.ProbeFailed.Code, "a failed query is AW-EVIDENCE-014");
        Expect.That(all.Error.Message.Contains("does not exist", StringComparison.Ordinal), "the message says why");
    }
}
