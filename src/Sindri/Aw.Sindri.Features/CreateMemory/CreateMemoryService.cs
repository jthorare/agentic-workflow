using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Features.CreateMemory;

/// <summary>Makes a memory. A bad name is SND-MEM-006, a taken one SND-MEM-002. The actor is the caller's.</summary>
/// <param name="store">The memories.</param>
/// <param name="time">The clock.</param>
/// <param name="callers">Who is creating.</param>
internal sealed class CreateMemoryService(IMemoryStore store, TimeProvider time, ICallerContextAccessor callers) : IService<CreateMemoryRequest, CreateMemoryResponse>
{
    private readonly IMemoryStore _store = store;
    private readonly TimeProvider _time = time;
    private readonly ICallerContextAccessor _callers = callers;

    public ValueTask<Result<CreateMemoryResponse>> Handle(CreateMemoryRequest request, CancellationToken cancellationToken) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = (_store, _time, _callers);
        return new NotSupportedException();
    }
}
