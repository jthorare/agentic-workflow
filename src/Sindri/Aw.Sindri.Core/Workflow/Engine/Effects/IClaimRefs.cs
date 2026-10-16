using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>
/// The claim's marker ref in the item's repo (port; engine spec §12 'effect.claim.ref'; engine P6 unit (d)): a ref
/// outside 'refs/heads', pointing at the default branch's head. Branches and the working tree are left alone.
/// </summary>
public interface IClaimRefs
{
    /// <summary>Creates <paramref name="reference"/> in <paramref name="repository"/> at its default branch's head, unless it exists.</summary>
    /// <param name="repository">The repo's checkout path (the profile's 'repos' entry).</param>
    /// <param name="reference">The full ref name, 'refs/sindri/{user}/{item}'.</param>
    /// <param name="cancellationToken">Cancels the git calls.</param>
    /// <returns>True when it was created now, false when it already existed; an error when the repo has no commit or git fails.</returns>
    public ValueTask<Result<bool>> Mark(string repository, string reference, CancellationToken cancellationToken);
}
