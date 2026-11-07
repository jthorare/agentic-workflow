using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.SuggestLinks;

/// <summary>Red stub for Linear hub Ph4's suggestions (spec §6.1); the ports arrive with the implementation.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Red stub: registered when the implementation lands; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can't see.")]
internal sealed class SuggestLinksService : IService<SuggestLinksRequest, SuggestLinksResponse>
{
    public ValueTask<Result<SuggestLinksResponse>> Handle(SuggestLinksRequest request, CancellationToken cancellationToken) => throw new NotSupportedException("red: SuggestLinksService");
}
