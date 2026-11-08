using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Features.ForgetMemoryFact;

/// <summary>Deletes facts by key, by tag or the expired ones. No selector or more than one is SND-MEM-003's sibling, a plain invalid input; an unknown memory is SND-MEM-001.</summary>
/// <param name="store">The memories.</param>
/// <param name="time">The clock, for 'expired'.</param>
internal sealed class ForgetMemoryFactService(IMemoryStore store, TimeProvider time) : IService<ForgetMemoryFactRequest, ForgetMemoryFactResponse>
{
    private readonly IMemoryStore _store = store;
    private readonly TimeProvider _time = time;

    public async ValueTask<Result<ForgetMemoryFactResponse>> Handle(ForgetMemoryFactRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = MemoryName.Parse(request.Name);
        if (name.IsError)
        {
            return name.Error;
        }

        var chosen = new[] { !string.IsNullOrWhiteSpace(request.Fact), !string.IsNullOrWhiteSpace(request.Tag), request.Expired }.Count(static c => c);
        if (chosen != 1)
        {
            return SindriErrors.InvalidInput.With("Forget needs exactly one of a fact id, a tag or expired.");
        }

        var selector = request.Expired ? FactSelector.Expired(_time.GetUtcNow())
            : !string.IsNullOrWhiteSpace(request.Fact) ? FactSelector.ByKey(request.Fact)
            : FactSelector.ByTag(request.Tag!.Trim().ToLowerInvariant());   // why: exactly one selector was chosen and it is neither the fact nor expired, so the tag is set
        var deleted = await _store.Delete(name.Value, selector, cancellationToken).ConfigureAwait(false);
        return deleted.IsError ? deleted.Error : new ForgetMemoryFactResponse(deleted.Value);
    }
}
