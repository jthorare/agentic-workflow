using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'vulnerabilities' block: asks an <see cref="IVulnerabilityProvider"/> about packages, or about a repo's lockfiles.</summary>
public sealed class VulnerabilitiesBlock(IEnumerable<IVulnerabilityProvider> providers, ILockfileSource lockfiles) : IGraphBlock
{
    /// <inheritdoc />
    public string Type => "vulnerabilities";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config => throw new NotSupportedException();

    /// <inheritdoc />
    public string Example => throw new NotSupportedException();

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken) => throw new NotSupportedException(providers.GetType().Name + lockfiles.GetType().Name);
}
