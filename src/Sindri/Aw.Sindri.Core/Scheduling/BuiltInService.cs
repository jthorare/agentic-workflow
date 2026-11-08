using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Scheduling;

/// <summary>
/// A background service the build runs without a profile entry (engine spec §7.2 S1-D8), for example the workflow
/// engine's event bridge. Registered next to its <see cref="IScheduledService"/>; a profile entry of the same name sets
/// its cadence instead.
/// </summary>
/// <param name="Name">The service, as <see cref="IScheduledService.Identity"/> names it.</param>
/// <param name="Cadence">How often it runs when the profile doesn't say.</param>
/// <param name="Description">One sentence on what it does, served on GET /sindri/services; null says nothing.</param>
public sealed record BuiltInService(ServiceName Name, Cadence Cadence, string? Description = null)
{
    /// <summary>Whether <paramref name="name"/> is a built-in: the build has it and the approved profile does not name it (a profile entry of the same name wins).</summary>
    /// <param name="name">The service.</param>
    /// <param name="namedByProfile">True when the approved profile names the service.</param>
    /// <param name="builtIns">The build's built-in services.</param>
    /// <returns>True for a built-in.</returns>
    public static bool Is(ServiceName name, bool namedByProfile, IEnumerable<BuiltInService> builtIns)
    {
        ArgumentNullException.ThrowIfNull(builtIns);
        return !namedByProfile && builtIns.Any(b => b.Name == name);
    }
}
