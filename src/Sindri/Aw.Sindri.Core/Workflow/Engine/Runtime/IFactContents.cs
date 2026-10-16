using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// Reads a stored fact's content back (port; engine spec rev 8.1 §5.3, §12; engine P6 unit (d)): what an effect that
/// needs a fact's value reads, such as 'effect.claim.ref' reading 'fact:item' for its repo.
/// </summary>
public interface IFactContents
{
    /// <summary>The content stored under <paramref name="fact"/>'s SHA-256.</summary>
    /// <param name="fact">The stored fact.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The content; an error when it isn't stored or can't be read.</returns>
    public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken);
}
