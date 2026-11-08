using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features.CreateMemory;

/// <summary>Makes a memory. A bad name is SND-MEM-006, a taken one SND-MEM-002. The actor is the caller's.</summary>
/// <param name="store">The memories.</param>
/// <param name="scrubber">Redacts the description before it is stored.</param>
/// <param name="time">The clock.</param>
/// <param name="callers">Who is creating.</param>
internal sealed class CreateMemoryService(IMemoryStore store, ITextScrubber scrubber, TimeProvider time, ICallerContextAccessor callers) : IService<CreateMemoryRequest, CreateMemoryResponse>
{
    private readonly IMemoryStore _store = store;
    private readonly ITextScrubber _scrubber = scrubber;
    private readonly TimeProvider _time = time;
    private readonly ICallerContextAccessor _callers = callers;

    public async ValueTask<Result<CreateMemoryResponse>> Handle(CreateMemoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = MemoryName.Parse(request.Name);
        if (name.IsError)
        {
            return name.Error;
        }

        var memory = new MemoryInfo(name.Value, string.IsNullOrWhiteSpace(request.Description) ? null : _scrubber.Scrub(request.Description.Trim()), _callers.Current?.Actor ?? "service:host", _time.GetUtcNow());
        var created = await _store.Create(memory, cancellationToken).ConfigureAwait(false);
        return created.IsError ? created.Error : new CreateMemoryResponse(MemoryView.From(memory, 0));
    }
}
