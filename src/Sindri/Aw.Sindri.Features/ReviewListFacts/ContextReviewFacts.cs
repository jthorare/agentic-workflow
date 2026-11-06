using System.Globalization;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
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
/// <param name="engine">Context's facade: settings, sync progress and the library.</param>
/// <param name="store">The record store the PR records are listed from.</param>
/// <param name="library">The library header's state.</param>
internal sealed class ContextReviewFacts(IApprovedProfile approvals, ICallerContextAccessor callers, IContextEngine engine, IRecordStore store, IReviewLibraryState library) : IReviewFacts
{
    private const int PageSize = 500;
    private const string GithubSource = "github";
    private const string LibrarySource = "library";
    private const string PullKeyPrefix = "github:pr/";

    public async ValueTask<Result<ReviewFactsSnapshot>> Read(CancellationToken cancellationToken)
    {
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("the Reviews list needs a caller");
        }

        var read = await ReadScope(cancellationToken).ConfigureAwait(false);
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

        var libraryFacts = await library.Read(audience, progress.Value.FirstOrDefault(static p => p.Source.Value == LibrarySource), cancellationToken).ConfigureAwait(false);
        var github = progress.Value.FirstOrDefault(static p => p.Source.Value == GithubSource);
        return new ReviewFactsSnapshot(pulls.Value, github is null ? null : new ProjectSourceFreshness(github.Source.Value, github.Usable, github.LastSync, github.LastCode), libraryFacts);
    }

    private async ValueTask<Result<Scoped>> ReadScope(CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is not { } loaded)
        {
            return approved.IsError ? approved.Error : SindriErrors.ProfileNotApproved.With("no profile is approved (fix: sindri profile approve)");
        }

        // why: the audience rule needs the repos' names, not each repo's index globs, so the entries carry name and path only
        ProfileRepoEntry[] entries = [.. (loaded.Profile.Repos ?? []).Select(static r => new ProfileRepoEntry(r.Name, r.Path))];
        var settings = engine.CheckSettings(loaded.Profile.Context, entries);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var scope = new ContextScope(loaded.Profile.Context, ScopeRules.Usable(entries), Caller(callers.Current));
        var audience = await AudienceOf(settings.Value, scope, cancellationToken).ConfigureAwait(false);
        return audience.IsError ? audience.Error : new Scoped(settings.Value, scope, audience.Value);
    }

    private async ValueTask<Result<IReadOnlyList<ReviewPull>>> Pulls(IReadOnlyList<string> repos, AudienceScope audience, CancellationToken cancellationToken)
    {
        var pulls = new List<ReviewPull>();
        RecordKey? after = null;
        while (true)
        {
            var page = await store.List(new RecordFilter(SourceId.From(GithubSource), Repo: null, RecordKind.Pr), after, PageSize, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            foreach (var record in page.Value.Where(r => r.State is RecordState.Open or RecordState.Draft && audience.Allows(r.Audience, r.Key)))
            {
                if (Parse(record, repos) is { } pull)
                {
                    pulls.Add(pull with { Claims = await Claims(record.Key, audience, cancellationToken).ConfigureAwait(false) });
                }
            }

            if (page.Value.Count < PageSize)
            {
                return pulls;
            }

            after = page.Value[^1].Key;
        }
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

    private static async ValueTask<Result<AudienceScope>> AudienceOf(ContextSettings settings, ContextScope scope, CancellationToken cancellationToken)
    {
        var names = scope.Repos.Select(static r => r.Name).ToList();
        var policy = new ProfileAudiencePolicy(settings, names);
        IEnumerable<ContextRepoName> asked = scope.Caller.Kind == ContextCallerKind.Session ? (scope.Caller.Repo is { } own ? [own] : []) : names;
        var allowed = new HashSet<AudienceKey>();
        foreach (var repo in asked)
        {
            var one = await policy.For(repo, cancellationToken).ConfigureAwait(false);
            if (one.IsError)
            {
                return one.Error;
            }

            allowed.UnionWith(one.Value.Allowed);
        }

        return new AudienceScope(allowed);
    }

    private static ContextCaller Caller(CallerContext? caller) => caller switch
    {
        { Class: CallerClass.Human } => new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Http),
        { Class: CallerClass.Session } => new ContextCaller(ContextCallerKind.Session, caller.Repo is { } repo && ContextRepoName.TryFrom(repo.Value, out var name) ? name : null, ContextClient.Http),
        _ => new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service),
    };

    private sealed record Scoped(ContextSettings Settings, ContextScope Scope, AudienceScope Audience);
}
