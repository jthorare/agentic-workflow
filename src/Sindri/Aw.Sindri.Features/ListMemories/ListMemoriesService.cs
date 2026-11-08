using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Features.ListMemories;

/// <summary>Lists every memory as a view with its fact count.</summary>
/// <param name="store">The memories.</param>
internal sealed class ListMemoriesService(IMemoryStore store) : IService<ListMemoriesRequest, ListMemoriesResponse>
{
    private readonly IMemoryStore _store = store;

    public async ValueTask<Result<ListMemoriesResponse>> Handle(ListMemoriesRequest request, CancellationToken cancellationToken)
    {
        var memories = await _store.List(cancellationToken).ConfigureAwait(false);
        if (memories.IsError)
        {
            return memories.Error;
        }

        var counts = await _store.Counts(cancellationToken).ConfigureAwait(false);
        if (counts.IsError)
        {
            return counts.Error;
        }

        return new ListMemoriesResponse([.. memories.Value.Select(m => MemoryView.From(m, counts.Value.GetValueOrDefault(m.Name.Value)))]);
    }
}
