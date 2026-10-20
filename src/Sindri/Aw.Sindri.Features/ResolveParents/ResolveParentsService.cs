using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Packets;

namespace Aw.Sindri.Features.ResolveParents;

/// <summary>LN-PKT6: each parent hash resolves to a frozen packet, or the read is a mismatch (SND-LINE-001).</summary>
/// <param name="frozen">The freeze records.</param>
internal sealed class ResolveParentsService(IFrozenPackets frozen) : IService<ResolveParentsRequest, ResolveParentsResponse>
{
    public async ValueTask<Result<ResolveParentsResponse>> Handle(ResolveParentsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var records = new List<FrozenPacket>(request.Parents.Count);
        var unresolved = new List<string>();
        foreach (var parent in request.Parents)
        {
            var found = await frozen.Find(parent, cancellationToken).ConfigureAwait(false);
            if (found.IsError && found.Error.Kind != ErrorKind.Invalid)
            {
                return found.Error;   // the records could not be read: that is not a mismatch
            }

            if (found.IsError || found.Value is null)
            {
                unresolved.Add(parent);
            }
            else
            {
                records.Add(found.Value);
            }
        }

        return unresolved.Count == 0
            ? new ResolveParentsResponse(records)
            : LineErrors.PacketMismatch.With($"{string.Join(", ", unresolved)} resolve to no frozen packet (fix: re-run the station that made the packet; a packet is never edited)");
    }
}
