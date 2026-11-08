using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Core.Views;

/// <summary>
/// The 'services' view rows that still count: those of services the approved profile names or the build runs as built-ins (plan G5). A service an
/// approval dropped keeps its row (the views are a pure fold of the ledger) but leaves the list, and no failure of it degrades health; a failed built-in does,
/// like a profile service. The same set GET /services lists.
/// </summary>
public static class ApprovedServices
{
    /// <summary>The rows of <paramref name="rows"/> whose service <paramref name="approved"/> names or <paramref name="builtIns"/> lists.</summary>
    /// <param name="rows">The services view's rows.</param>
    /// <param name="approved">The approved profile, or null before the first approval.</param>
    /// <param name="builtIns">The build's built-in services.</param>
    /// <returns>The rows to show and to judge health by.</returns>
    public static IReadOnlyList<ServiceRow> Of(IReadOnlyList<ServiceRow> rows, LoadedProfile? approved, IEnumerable<BuiltInService> builtIns)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(builtIns);
        var own = builtIns.Select(static b => b.Name.Value).ToHashSet(StringComparer.Ordinal);
        var named = approved?.Profile.Services ?? SindriProfile.NoServices;
        return [.. rows.Where(r => named.ContainsKey(r.Name.Value) || own.Contains(r.Name.Value))];
    }
}
