using Aw.Kernel;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>'sindri index connect linear' (issue 1617), the human's: reads the API key at the terminal, checks it against Linear's teams and only then saves it.</summary>
/// <param name="prompts">The terminal.</param>
/// <param name="profiles">The live profile, for the teams and the endpoint.</param>
/// <param name="writer">Stores the key (the OS keychain, else an owner-only file).</param>
/// <param name="pointers">Reads the stored key back, to prove the pointer resolves.</param>
/// <param name="profile">Writes the pointer into the profile.</param>
/// <param name="readers">Makes a team reader for an endpoint and a key.</param>
internal sealed class LinearConnect(
    IOnyxPrompts prompts,
    IProfileSource profiles,
    ISecretWriter writer,
    ISecretPointers pointers,
    ISourceTokenProfileWriter profile,
    Func<Uri, string, ILinearTeamReader> readers)
{
    /// <summary>Connects Linear.</summary>
    /// <param name="human">Whether the caller is a person at their own terminal.</param>
    /// <param name="cancellationToken">Cancels the asking and the checks.</param>
    /// <returns>The pointer now in the profile and the teams checked, or an error.</returns>
    internal ValueTask<Result<LinearConnected>> Run(bool human, CancellationToken cancellationToken)
    {
        _ = (human, cancellationToken, prompts, profiles, writer, pointers, profile, readers);
        throw new NotSupportedException();
    }
}
