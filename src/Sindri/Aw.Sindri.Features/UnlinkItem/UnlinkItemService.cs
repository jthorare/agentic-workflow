using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.UnlinkItem;

/// <summary>Red stub for Linear hub Ph4's unlink command (spec §6.1); the ports arrive with the implementation.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Red stub: registered when the implementation lands; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can't see.")]
internal sealed class UnlinkItemService : IService<UnlinkItemRequest, UnlinkItemResponse>
{
    public ValueTask<Result<UnlinkItemResponse>> Handle(UnlinkItemRequest request, CancellationToken cancellationToken) => throw new NotSupportedException("red: UnlinkItemService");
}
