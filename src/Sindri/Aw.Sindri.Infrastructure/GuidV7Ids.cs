using Aw.Kernel.Ids;

namespace Aw.Sindri.Infrastructure;

/// <summary>Event and command ids: version 7 Guids, which sort by creation time.</summary>
internal sealed class GuidV7Ids(TimeProvider clock) : IIdGenerator
{
    public Guid NewId() => Guid.CreateVersion7(clock.GetUtcNow());
}
