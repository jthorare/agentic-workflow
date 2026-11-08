using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Features.ListMemories;

/// <summary>Lists every memory as a view with its fact count.</summary>
/// <param name="store">The memories.</param>
internal sealed class ListMemoriesService(IMemoryStore store) : IService<ListMemoriesRequest, ListMemoriesResponse>
{
    private readonly IMemoryStore _store = store;

    public ValueTask<Result<ListMemoriesResponse>> Handle(ListMemoriesRequest request, CancellationToken cancellationToken) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = _store;
        return new NotSupportedException();
    }
}
