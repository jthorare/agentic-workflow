using System.Globalization;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Projects.Facts;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.ReviewListFacts;

/// <summary>
/// The Reviews list's facts over Context (PR review page spec §6.1, §5.6): the open PR records of the repos in <c language="text">context.sources.github.repos</c> that the current caller's audiences allow, each with its
/// claims, the GitHub source's state and the library's counts. It reads stored data only; nothing calls GitHub. The audience rule is the one the projects facts apply: a human reads every repo of the profile,
/// a session its own repo's.
/// </summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="callers">The current caller.</param>
/// <param name="engine">Context's facade: settings, sync progress, the open PR records and the library.</param>
/// <param name="library">The library header's state.</param>
internal sealed class ContextReviewFacts(IApprovedProfile approvals, ICallerContextAccessor callers, IContextEngine engine, IReviewLibraryState library) : IReviewFacts
{
    private const string GithubSource = "github";
    private const string LibrarySource = "library";
    private const string PullKeyPrefix = "github:pr/";

    public async ValueTask<Result<ReviewFactsSnapshot>> Read(CancellationToken cancellationToken)
    {
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("the Reviews list needs a caller");
        }

        var read = await ReviewScopes.Read(approvals, callers, engine, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var (settings, scope, audience) = read.Value;
        var progress = await engine.Progress(scope, cancellationToken).ConfigureAwait(false);
        var pulls = progress.IsError ? progress.Error : await Pulls(settings.GitHub?.Repos ?? [], audience, cancellationToken).ConfigureAwait(false);
        if (pulls.IsError)
        {
            return pulls.Error;
        }

        var libraryFacts = await library.Read(settings, scope, audience, progress.Value.FirstOrDefault(static p => p.Source.Value == LibrarySource), cancellationToken).ConfigureAwait(false);
        var github = progress.Value.FirstOrDefault(static p => p.Source.Value == GithubSource);
        return new ReviewFactsSnapshot(pulls.Value, github is null ? null : new ProjectSourceFreshness(github.Source.Value, github.Usable, github.LastSync, github.LastCode), libraryFacts);
    }

    private async ValueTask<Result<IReadOnlyList<ReviewPull>>> Pulls(IReadOnlyList<string> repos, AudienceScope audience, CancellationToken cancellationToken)
    {
        var records = await engine.OpenPulls(audience, cancellationToken).ConfigureAwait(false);
        if (records.IsError)
        {
            return records.Error;
        }

        var pulls = new List<ReviewPull>();
        foreach (var record in records.Value)
        {
            if (Parse(record, repos) is { } pull)
            {
                pulls.Add(pull with { Claims = await Claims(record.Key, audience, cancellationToken).ConfigureAwait(false) });
            }
        }

        return pulls;
    }

    // why: the record key is github:pr/<owner>/<repo>/<number>, which names the GitHub repo even when the record's own repo is the profile's name for the checkout
    private static ReviewPull? Parse(StoredRecord record, IReadOnlyList<string> repos)
    {
        if (!TryKey(record.Key.Value, out var repo, out var number) || !repos.Contains(repo, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var (head, requested, reviews) = Reviewers(record.Pull);
        return new ReviewPull(
            repo, number, record.Title, record.Author ?? string.Empty, record.State == RecordState.Draft, record.CreatedAt ?? record.SyncedAt, record.UpdatedAt ?? record.CreatedAt ?? record.SyncedAt,
            head, requested, reviews, ReviewersRead: record.Pull is not null, Claims: [], record.Url);
    }

    private static (string? Head, IReadOnlyList<string> Requested, IReadOnlyList<ReviewedAt> Reviews) Reviewers(PullFacts? facts) =>
        facts is null ? (null, [], []) : (facts.HeadSha, facts.RequestedReviewers, [.. facts.Reviews.Select(static r => new ReviewedAt(r.Login, r.Commit, r.NewCommits))]);

    private static bool TryKey(string key, out string repo, out int number)
    {
        repo = string.Empty;
        number = 0;
        if (!key.StartsWith(PullKeyPrefix, StringComparison.Ordinal) || key[PullKeyPrefix.Length..].Split('/') is not [var owner, var name, var last])
        {
            return false;
        }

        repo = owner + "/" + name;
        return int.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    private async ValueTask<IReadOnlyList<ReviewClaim>> Claims(RecordKey pr, AudienceScope audience, CancellationToken cancellationToken)
    {
        var claims = await engine.Claims(pr, audience, cancellationToken).ConfigureAwait(false);
        return claims.IsError ? [] : [.. claims.Value.Select(static c => new ReviewClaim(c.Resolution.Ref, c.Resolution.Status == LibraryRefStatus.Resolved))];
    }
}
