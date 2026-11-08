using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>
/// The items and pull requests of a list's issues. An item is linked to a record in the 'items' view (its Link); its pull request is the newest <c language="text">pr</c> row the 'item-links' view holds for it, when it has one,
/// shown with the Reviews list's word for it: <b>waiting</b> when the stored PR record waits on the owner (the 'Waiting on you' group's rule), <b>open</b> for any other stored open PR, <b>closed</b> when the stored
/// open PRs no longer hold it, <b>unknown</b> when the stored PRs can't be read. Anything that can't be read leaves that issue without a PR; nothing here fails the list.
/// </summary>
/// <param name="views">The 'items' and 'item-links' views.</param>
/// <param name="pulls">The stored PR records.</param>
/// <param name="approvals">The approved profile, for the review login.</param>
internal sealed class ViewLinearItemLinks(IViewReader views, IReviewFacts pulls, IApprovedProfile approvals) : ILinearItemLinks
{
    public async ValueTask<Result<IReadOnlyDictionary<string, LinearIssueLinks>>> For(IReadOnlyList<string> records, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        var items = await views.List<ItemRow>(ViewNames.Items, cancellationToken).ConfigureAwait(false);
        if (items.IsError)
        {
            return items.Error;
        }

        var wanted = records.ToHashSet(StringComparer.Ordinal);
        var linked = items.Value
            .Select(static i => (Item: i, Record: i.Link?.Value ?? string.Empty))
            .Where(p => wanted.Contains(p.Record))
            .GroupBy(static p => p.Record, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.Select(static p => p.Item).OrderByDescending(static i => i.UpdatedAt).First(), StringComparer.Ordinal);
        var links = await views.List<ItemLinkRow>(ViewNames.ItemLinks, cancellationToken).ConfigureAwait(false);
        var prs = Prs(linked, links.IsError ? [] : links.Value);   // unreadable links leave the issues without a PR chip; the list still shows
        var words = prs.Count == 0 ? null : await ReviewStates(cancellationToken).ConfigureAwait(false);
        return linked.ToDictionary(
            static l => l.Key,
            l => new LinearIssueLinks(
                new LinearItemLinkView(l.Value.Id.Value, ItemStateNames.Of(l.Value.State)),
                prs.TryGetValue(l.Key, out var p) ? new LinearPrView(p.Number, p.Url, words?.Word(p.Repo, p.Number) ?? "unknown") : null),
            StringComparer.Ordinal);
    }

    private static Dictionary<string, (int Number, string Url, string Repo)> Prs(Dictionary<string, ItemRow> linked, IReadOnlyList<ItemLinkRow> links)
    {
        var prs = new Dictionary<string, (int Number, string Url, string Repo)>(StringComparer.Ordinal);
        foreach (var (record, item) in linked)
        {
            var newest = links.Where(l => l.Kind == ItemLinkKind.Pr && l.Item == item.Id).OrderByDescending(static l => l.UpdatedAt).ThenByDescending(static l => l.Sequence);
            if (newest.Select(Pr).FirstOrDefault(static p => p is not null) is { } pr)
            {
                prs[record] = pr;
            }
        }

        return prs;
    }

    // the forge link when the row has one; else the short name 'owner/repo#number'
    private static (int Number, string Url, string Repo)? Pr(ItemLinkRow row) => Parse(row.Url) ?? Short(row);

    private static (int Number, string Url, string Repo)? Short(ItemLinkRow row)
    {
        var hash = row.Ref.LastIndexOf('#');
        return hash > 0 && row.Url is { } url && int.TryParse(row.Ref.AsSpan(hash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? (number, url, row.Ref[..hash]) : null;
    }

    // a forge PR link: https://<host>/<owner>/<repo>/pull/<number>
    private static (int Number, string Url, string Repo)? Parse(string? link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 4 && parts[^2] == "pull" && int.TryParse(parts[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? (number, uri.AbsoluteUri, parts[^4] + "/" + parts[^3])
            : null;
    }

    private async ValueTask<ReviewWords?> ReviewStates(CancellationToken cancellationToken)
    {
        var read = await pulls.Read(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return null;
        }

        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        var login = approved.IsSuccess ? approved.Value?.Profile.Review?.Login : null;
        return new ReviewWords(read.Value.Pulls, login);
    }

    private sealed class ReviewWords(IReadOnlyList<ReviewPull> pulls, string? login)
    {
        internal string Word(string repo, int number)
        {
            var pull = pulls.FirstOrDefault(p => string.Equals(p.Repo, repo, StringComparison.OrdinalIgnoreCase) && p.Number == number);
            return pull is null ? "closed" : login is not null && ReviewListBuilder.Classify(login, pull) is not null ? "waiting" : "open";
        }
    }
}
