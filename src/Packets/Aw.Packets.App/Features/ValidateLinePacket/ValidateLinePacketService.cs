using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.App.Features.ValidateLinePacket;

internal sealed class ValidateLinePacketService(LinePacketInspector inspector, ISizesSource sizeSource, ISealSource sealSource) : IService<ValidateLinePacketRequest, ValidateLinePacketResponse>
{
    public async ValueTask<Result<ValidateLinePacketResponse>> Handle(ValidateLinePacketRequest request, CancellationToken cancellationToken)
    {
        var kind = PacketKinds.Find(request.Kind)!;   // why: the validator accepted the kind
        SizesFile? sizes = null;
        if (request.SizesFile is { } path)
        {
            var read = await sizeSource.Read(path, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            sizes = read.Value;
        }

        PacketHash? sealedSet = null;
        if (request.SealFile is { } sealPath)
        {
            var read = await sealSource.Read(sealPath, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            sealedSet = read.Value;
        }

        var inspection = await inspector.Inspect(kind, request.PacketDirectory, request.RepoDirectory ?? request.PacketDirectory, new LineInspectOptions(sizes, sealedSet), cancellationToken)
            .ConfigureAwait(false);
        return inspection.Map(static i => new ValidateLinePacketResponse(1, i.Kind.Name, i.Manifest?.Id, i.Passed, i.Checks));
    }
}
