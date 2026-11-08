using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features.WriteMemoryFact;

/// <summary>
/// Writes a fact. The text is checked (SND-MEM-003), scrubbed and then embedded (a missing model stores the fact with no vector). A fact whose vector is at or above
/// <see cref="MemoryDedupe.Threshold"/> of an unexpired fact with the same tags refreshes that fact (action 'refreshed', one row); otherwise it is added.
/// </summary>
/// <param name="store">The memories.</param>
/// <param name="embedder">Embeds the scrubbed text.</param>
/// <param name="scrubber">Redacts the text before anything stores it.</param>
/// <param name="time">The clock.</param>
/// <param name="ids">The id source for a new fact's key.</param>
internal sealed class WriteMemoryFactService(IMemoryStore store, IMemoryEmbedder embedder, ITextScrubber scrubber, TimeProvider time, IIdGenerator ids) : IService<WriteMemoryFactRequest, WriteMemoryFactResponse>
{
    private readonly IMemoryStore _store = store;
    private readonly IMemoryEmbedder _embedder = embedder;
    private readonly ITextScrubber _scrubber = scrubber;
    private readonly TimeProvider _time = time;
    private readonly IIdGenerator _ids = ids;

    public ValueTask<Result<WriteMemoryFactResponse>> Handle(WriteMemoryFactRequest request, CancellationToken cancellationToken) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = (_store, _embedder, _scrubber, _time, _ids);
        return new NotSupportedException();
    }
}
