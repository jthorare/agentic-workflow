using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.Items;

/// <summary>Resolves an item reference through the views: an alias through 'item-aliases', then the id in 'items'.</summary>
internal sealed class ViewItemResolver : IItemResolver
{
    private readonly IViewReader _views;

    /// <summary>Resolves through <paramref name="views"/>.</summary>
    /// <param name="views">The views reader.</param>
    public ViewItemResolver(IViewReader views)
    {
        ArgumentNullException.ThrowIfNull(views);
        _views = views;
    }

    public async ValueTask<Result<ItemRow>> Resolve(ItemRef reference, CallerContext caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var id = reference.Id;
        if (id is null)
        {
            var alias = await _views.Get<AliasRow>(ViewNames.ItemAliases, reference.ToString(), cancellationToken).ConfigureAwait(false);
            if (alias.IsError)
            {
                return alias.Error;
            }

            id = alias.Value?.Item;
        }

        if (id is null)
        {
            return NotFound(reference);
        }

        var row = await _views.Get<ItemRow>(ViewNames.Items, id.Value.Value, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        return row.Value is { } item ? item : NotFound(reference);
    }

    private static Error NotFound(ItemRef reference) => SindriErrors.ItemNotFound.With($"no item '{reference}'");
}
