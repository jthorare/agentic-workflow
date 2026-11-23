using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.MarkAllRead;

/// <summary>A stub until M1h (messages spec §14): Mark all read and its Undo.</summary>
internal sealed class MarkAllReadService : IService<MarkAllReadRequest, MarkAllReadResponse>
{
    public ValueTask<Result<MarkAllReadResponse>> Handle(MarkAllReadRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("MarkAllRead is not built yet: messages spec §14 phase M1h");
}
