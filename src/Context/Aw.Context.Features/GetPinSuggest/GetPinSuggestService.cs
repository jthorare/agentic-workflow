using Aw.Context.Core.Concepts;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetPinSuggest;

/// <summary>The pin-suggest read (concept pins spec §3.6).</summary>
internal sealed class GetPinSuggestService : IService<GetPinSuggestRequest, PinSuggestView>
{
    public ValueTask<Result<PinSuggestView>> Handle(GetPinSuggestRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("pin suggest is not built yet");
}
