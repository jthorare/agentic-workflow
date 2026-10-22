using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;
using Aw.Packets.Core.Kinds;

namespace Aw.Packets.App.Features.ValidateLinePacket;

internal sealed class ValidateLinePacketValidator : IValidator<ValidateLinePacketRequest>
{
    public Result<Unit> Validate(ValidateLinePacketRequest value) =>
        PacketKinds.Find(value.Kind) is not { IsLine: true } || string.IsNullOrWhiteSpace(value.PacketDirectory) || value.RepoDirectory is { Length: 0 } || value.SizesFile is { Length: 0 } || value.SealFile is { Length: 0 }
            ? PacketsErrors.Invalid.With("The kind must be a line kind, and the packet directory, and the repository directory and the sizes file and the seal file when given, must not be empty.")
            : Unit.Value;
}
