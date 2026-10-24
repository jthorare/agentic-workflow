using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ReadLedger;

/// <summary>
/// Pages the ledger for audits (API spec §2.2, §3.1): the same public envelopes as the event stream, scrubbed and escaped, with
/// the types outside the catalog's public ones too and every payload whole, for every caller. The
/// next cursor is how far the read looked, set after a full page or while the read stopped short of the head.
/// </summary>
/// <param name="ledger">The ledger, for its head.</param>
/// <param name="view">What the caller sees of a row.</param>
/// <param name="items">Resolves an item named by alias.</param>
/// <param name="repos">Resolves an item named by id, from the ledger.</param>
/// <param name="callers">The caller.</param>
internal sealed class ReadLedgerService(ILedgerReader ledger, CallerEventView view, IItemResolver items, IItemRepos repos, ICallerContextAccessor callers)
    : IService<ReadLedgerRequest, LedgerPage>
{
    private const int MaxPage = 10_000;

    public async ValueTask<Result<LedgerPage>> Handle(ReadLedgerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var checkedInput = await Check(request, cancellationToken).ConfigureAwait(false);
        if (checkedInput.IsError)
        {
            return checkedInput.Error;
        }

        var (filter, head) = checkedInput.Value;
        var page = await view.Read(new EventQuery(request.After, request.Max, filter, IncludeNonPublic: true), cancellationToken).ConfigureAwait(false);
        return page.IsError ? page.Error : new LedgerPage(page.Value.Events, MoreAfter(page.Value, request.Max, head) ? page.Value.ScannedTo : null);
    }

    /// <summary>
    /// Whether the ledger may have more past the page: always after a full page (appends since the head was read may be past it), else when the read
    /// stopped short of that head.
    /// </summary>
    private static bool MoreAfter(ViewedPage page, int max, long head) => page.Events.Count >= max || page.ScannedTo < head;

    /// <summary>The ledger filter the request names and the head it reads against, or the 400 / 404 for a request that can't be served.</summary>
    private async ValueTask<Result<(LedgerFilter Filter, long Head)>> Check(ReadLedgerRequest request, CancellationToken cancellationToken)
    {
        if (request.Max is < 1 or > MaxPage)
        {
            return SindriErrors.InvalidInput.With($"max is 1 to {MaxPage} (fix: --max 100)");
        }

        TypeFilter? types = null;
        if (request.Type is { Length: > 0 } text)
        {
            var parsed = TypeFilter.Parse(text);
            if (parsed.IsError)
            {
                return parsed.Error;
            }

            types = parsed.Value;
        }

        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        if (request.After < 0)
        {
            return SindriErrors.InvalidInput.With(string.Create(CultureInfo.InvariantCulture, $"after {request.After} is negative; a cursor is 0 or more (fix: --after 0, or leave it out)"));
        }

        if (request.After > head.Value)
        {
            return SindriErrors.InvalidInput.With(string.Create(CultureInfo.InvariantCulture, $"after {request.After} is past the ledger's head {head.Value} (fix: --after {head.Value}, or leave it out)"));
        }

        var item = await ItemFilters.Resolve(request.Item, items, repos, callers, cancellationToken).ConfigureAwait(false);
        return item.IsError ? item.Error : (new LedgerFilter(types, item.Value), head.Value);
    }
}
