using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Kernel;

namespace Aw.Forge.Core.Ports;

/// <summary>
/// What only a fake or file forge can do: play the other side. Fixtures and proofs use it to put a comment, a failing check
/// or a foreign push where the babysitter will find it. The GitHub adapter does not implement it.
/// </summary>
public interface IForgeSeed
{
    /// <summary>Adds a conversation item.</summary>
    /// <param name="request">The item.</param>
    /// <returns>Its thread key.</returns>
    public Result<string> SeedThread(SeedThreadRequest request);

    /// <summary>Sets the checks on the head.</summary>
    /// <param name="request">The checks.</param>
    /// <returns>Success or the error.</returns>
    public Result<Unit> SeedChecks(SeedChecksRequest request);

    /// <summary>Moves the head branch as an out-of-band push would, clearing the checks.</summary>
    /// <param name="pull">The pull request.</param>
    /// <param name="sha">The new head.</param>
    /// <returns>Success or the error.</returns>
    public Result<Unit> AdvanceHead(ForgeRef pull, string sha);
}
