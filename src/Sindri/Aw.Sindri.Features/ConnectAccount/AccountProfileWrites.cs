using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.ConnectAccount;

/// <summary>
/// The profile side of connecting GitHub: sets `review.credential` and clears `setup.skipAccounts`, each the way `sindri profile set` does (comments and order kept, the edited text read and checked before the atomic write).
/// The first write creates the profile when there is none (first-run spec decision 5).
/// </summary>
/// <param name="writer">Plans and writes the edit.</param>
/// <param name="parser">Reads the edited text.</param>
/// <param name="initializer">Creates the default profile when there is none.</param>
/// <param name="shipped">The effect kinds a new profile allows.</param>
internal sealed class AccountProfileWrites(IProfileKeyWriter writer, IProfileTextParser parser, IProfileInitializer initializer, IShippedEffects shipped)
{
    /// <summary>Points `review.credential` at <paramref name="credential"/> and removes `setup.skipAccounts`.</summary>
    /// <param name="credential">The credential, such as `command:gh auth token`; never a token.</param>
    /// <param name="cancellationToken">Cancels the profile's creation.</param>
    /// <returns>Unit, or the writer's or parser's error.</returns>
    internal ValueTask<Result<Unit>> GitHub(string credential, CancellationToken cancellationToken)
    {
        GC.KeepAlive((writer, parser, initializer, shipped, credential, cancellationToken));
        throw new NotSupportedException("AccountProfileWrites is not built yet (first-run spec decision 14)");
    }
}
