using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.OpenItem;

/// <summary>The shape of an open (API spec §3.2): a mode that is one of the three, and an item version of at least 1 (items start at 1, so a lower one can only be a mistake, not a stale read). The target is already an <see cref="Core.Launch.AgentId"/>, so its pattern holds.</summary>
internal sealed class OpenItemValidator : IValidator<OpenItemRequest>
{
    public Result<Unit> Validate(OpenItemRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return !Enum.IsDefined(value.Mode)
            ? SindriErrors.InvalidInput.With(string.Create(CultureInfo.InvariantCulture, $"{(int)value.Mode} isn't an open mode (fix: open, attach or resume)"))
            : value.ItemVersion < 1
                ? SindriErrors.InvalidInput.With(string.Create(CultureInfo.InvariantCulture, $"itemVersion {value.ItemVersion} can't match an item (fix: the version item detail shows, from 1)"))
                : Unit.Value;
    }
}
