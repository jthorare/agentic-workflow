using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The '{instanceId}' route segment (engine spec §6.3): the instance's opaque id, 'WFI-…'. A CLI address never travels
/// in a route; the CLI resolves it to the id through 'GET /workflow/instances?address=' first.
/// </summary>
internal static class InstanceRoute
{
    /// <summary>The id a route names, checked; anything else (an address, a bare item) throws Vogen's validation exception, which the Host maps to a 400 (SND-CLI-003).</summary>
    /// <param name="instanceId">The route value.</param>
    /// <returns>The id, as the reference the slices resolve.</returns>
    internal static string Id(string instanceId) => WorkflowInstanceId.From(instanceId).Value;

    /// <summary>The route segment for a run token or another value a client writes into a route.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The escaped segment.</returns>
    internal static string Segment(string value) => Uri.EscapeDataString(value);
}
