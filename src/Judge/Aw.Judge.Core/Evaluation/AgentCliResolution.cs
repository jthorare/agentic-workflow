using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Evaluation;

/// <summary>Which agent CLIs the chain uses, in priority order.</summary>
public static class AgentCliResolution
{
    /// <summary>
    /// An explicit config list wins outright; otherwise the default order with the AW_PROVIDER host moved to the front.
    /// Either way deduplicated and filtered to binaries on the search path.
    /// </summary>
    /// <param name="locator">Finds the binaries.</param>
    /// <param name="hostProvider">AW_PROVIDER, or null when unset.</param>
    /// <param name="configured">The config file's agent-CLI list, or null when it has none.</param>
    /// <returns>The installed agent CLIs, in priority order.</returns>
    public static IReadOnlyList<AgentCliName> Resolve(IExecutableLocator locator, string? hostProvider, IReadOnlyList<AgentCliName>? configured)
    {
        ArgumentNullException.ThrowIfNull(locator);
        var order = configured ?? Preferred(hostProvider);
        return [.. order.Distinct().Where(name => locator.IsOnPath(AgentCliNames.Binary(name)))];
    }

    private static IReadOnlyList<AgentCliName> Preferred(string? hostProvider) =>
        AgentCliNames.FromHost(hostProvider) is { } first
            ? [first, .. AgentCliNames.DefaultOrder.Where(n => n != first)]
            : AgentCliNames.DefaultOrder;
}
