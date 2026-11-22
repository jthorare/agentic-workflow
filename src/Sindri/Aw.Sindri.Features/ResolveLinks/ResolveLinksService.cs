using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.ResolveLinks;

/// <summary>
/// Resolves the links of a message window to rows (messages spec §9.3, #3021): each url's shape gives a row key (<see cref="LinkShapes"/>), and the local
/// store gives the row's fields. A url with no known shape, or a key the store holds no row for, answers 'none', so the link stays plain text. One answer
/// per url, in the request's order; a repeated url gets the same answer each time. The store is read once, for the distinct keys, and never when no url
/// has a shape. Nothing calls GitHub, Linear or Slack. One to 20 urls.
/// </summary>
/// <param name="rows">The local rows.</param>
internal sealed class ResolveLinksService(ILinkRows rows) : IService<ResolveLinksRequest, ResolveLinksResponse>
{
    public async ValueTask<Result<ResolveLinksResponse>> Handle(ResolveLinksRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Urls is null || request.Urls.Count is 0 or > LinkShapes.MaxUrls || request.Urls.Any(static u => u is null))
        {
            return SindriErrors.InvalidQuery.With(string.Create(CultureInfo.InvariantCulture, $"'urls' is a list of 1 to {LinkShapes.MaxUrls} urls, none of them null (fix: send at most one window's links)"));
        }

        var keys = request.Urls.Select(LinkShapes.RowKey).ToList();
        var wanted = keys.OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        if (wanted.Count == 0)
        {
            return new ResolveLinksResponse([.. request.Urls.Select(None)]);
        }

        var found = await rows.Find(wanted, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        return new ResolveLinksResponse([.. request.Urls.Select((url, i) => keys[i] is { } key && found.Value.TryGetValue(key, out var row) ? Hit(url, row) : None(url))]);
    }

    private static ResolvedLink Hit(Uri url, LinkRow row) => new(url, row.Key, row.Title, row.State)
    {
        Ci = row.Ci,
        WaitsOn = row.WaitsOn,
        Since = row.Since,
    };

    private static ResolvedLink None(Uri url) => new(url, RowKey: null, Title: null, State: null);
}
