using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>
/// The items and pull requests of a list's issues. An item is linked to a record in the 'items' view (its Link); its pull request is the <c language="text">pr</c> fact of the item's workflow instance, when it has one,
/// shown with the Reviews list's word for it: <b>waiting</b> when the stored PR record waits on the owner (the 'Waiting on you' group's rule), <b>open</b> for any other stored open PR, <b>closed</b> when the stored
/// open PRs no longer hold it. Anything that can't be read leaves that issue without a PR; nothing here fails the list.
/// </summary>
/// <param name="views">The 'items' view.</param>
/// <param name="instances">The workflow instances.</param>
/// <param name="contents">The stored fact contents.</param>
/// <param name="pulls">The stored PR records.</param>
/// <param name="approvals">The approved profile, for the review login.</param>
internal sealed class ViewLinearItemLinks(IViewReader views, IInstanceReader instances, IFactContents contents, IReviewFacts pulls, IApprovedProfile approvals) : ILinearItemLinks
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
        var prs = await Prs(linked, cancellationToken).ConfigureAwait(false);
        var words = prs.Count == 0 ? null : await ReviewStates(cancellationToken).ConfigureAwait(false);
        return linked.ToDictionary(
            static l => l.Key,
            l => new LinearIssueLinks(
                new LinearItemLinkView(l.Value.Id.Value, ItemStateNames.Of(l.Value.State)),
                prs.TryGetValue(l.Key, out var p) ? new LinearPrView(p.Number, p.Url, words?.Word(p.Repo, p.Number) ?? "open") : null),
            StringComparer.Ordinal);
    }

    private async ValueTask<Dictionary<string, (int Number, string Url, string Repo)>> Prs(Dictionary<string, ItemRow> linked, CancellationToken cancellationToken)
    {
        var prs = new Dictionary<string, (int Number, string Url, string Repo)>(StringComparer.Ordinal);
        foreach (var (record, item) in linked)
        {
            if (await Pr(item, cancellationToken).ConfigureAwait(false) is { } pr)
            {
                prs[record] = pr;
            }
        }

        return prs;
    }

    private async ValueTask<(int Number, string Url, string Repo)?> Pr(ItemRow item, CancellationToken cancellationToken)
    {
        if (item.Instance is not { } text || !InstanceAddress.TryParse(text, provider: null, out var address))
        {
            return null;
        }

        var view = await instances.Get(address, cancellationToken).ConfigureAwait(false);
        if (view.IsError || view.Value.FactShas?.GetValueOrDefault("fact:pr") is not { } sha)
        {
            return null;
        }

        var bytes = await contents.Read(new FactValue("fact:sha256:" + sha, sha, HeadSha: null), cancellationToken).ConfigureAwait(false);
        if (bytes.IsError)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(bytes.Value);
            var root = document.RootElement;
            var link = root.ValueKind == JsonValueKind.String ? root.GetString() : Named(root, "url") ?? Named(root, "link") ?? Named(root, "pr");
            return Parse(link);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Named(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

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
