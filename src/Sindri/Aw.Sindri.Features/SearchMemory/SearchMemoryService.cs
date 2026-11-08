using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Features.SearchMemory;

/// <summary>
/// Recalls facts. Unexpired facts only. It embeds the query and ranks by cosine at the level's floor; when the embedder says Unavailable it ranks by keywords
/// and answers mode 'keyword' with the reason. An unknown memory is SND-MEM-001 and a bad level SND-MEM-005.
/// </summary>
/// <param name="store">The memories.</param>
/// <param name="embedder">Embeds the query.</param>
/// <param name="time">The clock, for expiry.</param>
internal sealed class SearchMemoryService(IMemoryStore store, IMemoryEmbedder embedder, TimeProvider time) : IService<SearchMemoryRequest, SearchMemoryResponse>
{
    private readonly IMemoryStore _store = store;
    private readonly IMemoryEmbedder _embedder = embedder;
    private readonly TimeProvider _time = time;

    public ValueTask<Result<SearchMemoryResponse>> Handle(SearchMemoryRequest request, CancellationToken cancellationToken) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = (_store, _embedder, _time);
        return new NotSupportedException();
    }
}
