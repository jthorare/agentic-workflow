using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Features.ForgetMemoryFact;

/// <summary>Deletes facts by key, by tag or the expired ones. No selector or more than one is SND-MEM-003's sibling, a plain invalid input; an unknown memory is SND-MEM-001.</summary>
/// <param name="store">The memories.</param>
/// <param name="time">The clock, for 'expired'.</param>
internal sealed class ForgetMemoryFactService(IMemoryStore store, TimeProvider time) : IService<ForgetMemoryFactRequest, ForgetMemoryFactResponse>
{
    private readonly IMemoryStore _store = store;
    private readonly TimeProvider _time = time;

    public ValueTask<Result<ForgetMemoryFactResponse>> Handle(ForgetMemoryFactRequest request, CancellationToken cancellationToken) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = (_store, _time);
        return new NotSupportedException();
    }
}
