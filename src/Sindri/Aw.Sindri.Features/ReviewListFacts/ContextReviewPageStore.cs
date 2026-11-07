using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Records;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.ReviewListFacts;

/// <summary>
/// The PR page's stored side over Context (PR review page spec §6.2, §4.4): the PR's record and the library's view of its claims (<see cref="ReviewPageLibraryReader"/>). The audience rule is the list's (<see cref="ReviewScopes"/>):
/// a session reads its own repo's docs and is told how many claimed docs it may not read. A repo outside <c language="text">context.sources.github.repos</c> is SND-REVIEW-004.
/// </summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="callers">The current caller.</param>
/// <param name="engine">Context's facade.</param>
/// <param name="library">The claims and the doc map.</param>
internal sealed partial class ContextReviewPageStore(IApprovedProfile approvals, ICallerContextAccessor callers, IContextEngine engine, ReviewPageLibraryReader library) : IReviewPageStore
{
    public async ValueTask<Result<ReviewPageStored>> Read(ReviewPullRef pull, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pull);
        var read = await ReviewScopes.Read(approvals, callers, engine, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var (settings, scope, audience) = read.Value;
        var slug = pull.Owner + "/" + pull.Repo;
        if (!(settings.GitHub?.Repos ?? []).Contains(slug, StringComparer.OrdinalIgnoreCase))
        {
            return SindriErrors.ReviewNotSynced.With($"Sindri doesn't sync {slug}. Add it to `context.sources.github.repos`.");
        }

        var records = await engine.OpenPulls(audience, cancellationToken).ConfigureAwait(false);
        if (records.IsError)
        {
            return records.Error;
        }

        var key = "github:pr/" + slug + "/" + pull.Number.ToString(CultureInfo.InvariantCulture);
        var record = records.Value.FirstOrDefault(r => string.Equals(r.Key.Value, key, StringComparison.OrdinalIgnoreCase));
        var facts = await library.Read(new ReviewPageLibraryAsk(settings, scope, audience, slug, record), cancellationToken).ConfigureAwait(false);
        return record is null
            ? new ReviewPageStored(Title: null, Author: null, Draft: false, Url: null, UpdatedAt: null, IssueKeys: [], facts)
            : new ReviewPageStored(record.Title, record.Author, record.State == RecordState.Draft, record.Url, record.UpdatedAt ?? record.CreatedAt ?? record.SyncedAt, IssueKeys(record), facts);
    }

    private static string[] IssueKeys(StoredRecord record) =>
        [.. record.Links.Select(static l => IssueKey.Match(l.ToRef)).Where(static m => m.Success).Select(static m => m.Groups["key"].Value).Distinct(StringComparer.Ordinal)];

    [GeneratedRegex("^linear:[a-z-]*/(?<key>[A-Z][A-Z0-9]{1,9}-[0-9]+)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IssueKey { get; }
}
