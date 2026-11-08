using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The block catalog of this build: empty until the engine's blocks arrive (issue 2258, PR 6 and 9), so every node type is unknown to the validator for now.</summary>
internal sealed class EmptyGraphBlockCatalog : IGraphBlockCatalog
{
    public IReadOnlyDictionary<string, IGraphBlockType> Types { get; } = new Dictionary<string, IGraphBlockType>(StringComparer.Ordinal);
}
