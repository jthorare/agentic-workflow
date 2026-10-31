using Aw.Context.Core.Concepts;
using Aw.Context.Core.Map;
using Aw.Context.Core.Pages;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetPages;

/// <summary>One slice of the pages of the map the caller may read, with their actions and concepts, built by <see cref="PageReader"/> (reads never call a model: the pages are the owner's overlay and the map's surfaces).</summary>
internal sealed class GetPagesService(ConceptReader reader) : IService<GetPagesRequest, PagesView>
{
    public async ValueTask<Result<PagesView>> Handle(GetPagesRequest request, CancellationToken cancellationToken)
    {
        var query = request.Query;
        var mapRead = MapRead.Of(query.Draft, query.MapVersion);
        if (mapRead.IsError)
        {
            return mapRead.Error;
        }

        // The repo filter is the pages' own (an unknown repo is SND-CONTEXT-258), so the reader is not asked to cut the graph to it.
        var read = await reader.Read(request.Scope, mapRead.Value, repo: null, cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : PageReader.Build(query, read.Value.Basis.MapVersion, read.Value.Graph, request.Scope.Repos, read.Value.Text);
    }
}
