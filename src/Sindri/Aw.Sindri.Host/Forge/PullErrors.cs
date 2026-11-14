using Aw.Forge.Core;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Forge;

/// <summary>Maps the forge package's errors (AW-FORGE-nnn) onto Sindri's (SND-FORGE-nnn); the forge's code stays in the message.</summary>
internal static class PullErrors
{
    /// <summary>Maps one forge error.</summary>
    /// <param name="error">The error the forge port answered.</param>
    /// <returns>Sindri's error: refused for a host, credential or input the forge turns away, not found for a missing pull request, unavailable for everything else.</returns>
    internal static Error From(Error error)
    {
        var message = $"{error.Code}: {error.Message}";
        if (error.Code == ForgeErrors.NotFound.Code)
        {
            return SindriErrors.ForgeNotFound.With(message);
        }

        // why: a 403 for one repo is not a dead credential; its own code lets a caller skip the repo and keep reading the others
        if (error.Code == ForgeErrors.Refused.Code)
        {
            return SindriErrors.ForgeRepoRefused.With(message);
        }

        return error.Code == ForgeErrors.Invalid.Code || error.Code == ForgeErrors.HostNotAllowed.Code
            || error.Code == ForgeErrors.Unauthorized.Code || error.Code == ForgeErrors.Rejected.Code
            ? SindriErrors.ForgeRefused.With(message)
            : SindriErrors.ForgeUnavailable.With(message);
    }
}
