using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>The Steps this build has a block runtime for (DT spec §8.2), from the AddBlockStep registrations. A singleton.</summary>
public sealed class BlockStepRuntimes
{
    /// <summary>Collects the registrations.</summary>
    /// <param name="registrations">Every AddBlockStep registration.</param>
    public BlockStepRuntimes(IEnumerable<BlockStepRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        Ids = registrations.Select(static r => r.Step).ToHashSet();
    }

    /// <summary>The registered Steps.</summary>
    public IReadOnlySet<BlockId> Ids { get; }
}
