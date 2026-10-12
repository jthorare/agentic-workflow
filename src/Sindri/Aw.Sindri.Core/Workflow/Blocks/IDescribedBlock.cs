namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>A block class's static descriptor (spec §2, W1): read at registration, so the catalog needs no instance.</summary>
public interface IDescribedBlock
{
    /// <summary>What the block declares about itself.</summary>
    public static abstract BlockDescriptor Descriptor { get; }
}
