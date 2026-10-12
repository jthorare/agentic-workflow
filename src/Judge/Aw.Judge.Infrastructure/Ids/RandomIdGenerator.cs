using Aw.Kernel.Ids;

namespace Aw.Judge.Infrastructure.Ids;

/// <summary>IIdGenerator over random version-4 GUIDs (the TS judge's crypto.randomUUID).</summary>
internal sealed class RandomIdGenerator : IIdGenerator
{
    /// <inheritdoc />
    public Guid NewId() => Guid.NewGuid();
}
