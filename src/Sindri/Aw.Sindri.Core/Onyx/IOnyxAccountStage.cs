using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>Runs Onyx setup steps 6 to 9 (the owner's sign-up, Sindri's dedicated user and token, the profile section) against an Onyx at a given origin, asking at the terminal; `onyx attach`'s way to them without naming the account slice.</summary>
public interface IOnyxAccountStage
{
    /// <summary>Runs the account stage.</summary>
    /// <param name="origin">Where Onyx answers; already checked against the environment.</param>
    /// <param name="version">The release the Onyx there reported.</param>
    /// <param name="cancellationToken">Cancels the waiting and the requests.</param>
    /// <returns>What each step did, or the error that stopped them.</returns>
    public ValueTask<Result<OnyxAccountStageResult>> Run(OnyxOrigin origin, string version, CancellationToken cancellationToken);
}
