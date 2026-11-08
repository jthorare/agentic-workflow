using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Features.GetEventCatalog;

/// <summary>
/// Lists the catalog's public types that map to at least one API view, sorted by type, each with its key shape and the views the app re-queries when it
/// arrives (UI spec E-15). A public type with no view is left out, never listed with an empty list: a consumer reads a type that isn't listed as unknown and
/// re-queries everything, where an empty list would say "re-query nothing" and leave its screens stale.
/// </summary>
internal sealed class GetEventCatalogService : IService<GetEventCatalogRequest, EventCatalogView>
{
    public ValueTask<Result<EventCatalogView>> Handle(GetEventCatalogRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var types = EventCatalog.All
            .Where(static e => e.Public && e.Invalidates.Count > 0)
            .OrderBy(static e => e.Type, StringComparer.Ordinal)
            .Select(static e => new EventTypeView(e.Type, e.KeyShape, e.Invalidates))
            .ToList();
        return ValueTask.FromResult<Result<EventCatalogView>>(new EventCatalogView(types));
    }
}
