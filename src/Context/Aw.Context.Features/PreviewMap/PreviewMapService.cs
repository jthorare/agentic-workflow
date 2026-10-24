using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.PreviewMap;

/// <summary>Reads and checks a map file without writing: what the owner confirms by its hash.</summary>
internal sealed class PreviewMapService(IMapFileReader reader) : IService<PreviewMapRequest, MapFilePreview>
{
    public async ValueTask<Result<MapFilePreview>> Handle(PreviewMapRequest request, CancellationToken cancellationToken)
    {
        var file = await reader.Read(request.Path, cancellationToken).ConfigureAwait(false);
        if (file.IsError)
        {
            return file.Error;
        }

        var map = MapDocumentValidator.Check(file.Value.Document, KeyMigrations.Shipped);
        if (map.IsError)
        {
            return map.Error;
        }

        if (MapAccess.Maps(map.Value.Repo, request.Scope) is { } unmapped)
        {
            return unmapped;
        }

        return new MapFilePreview(request.Path, map.Value.Repo, file.Value.Document.Kind, file.Value.Hash, map.Value.Counts);
    }
}
