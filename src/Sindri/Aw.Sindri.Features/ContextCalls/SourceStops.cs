using System.Globalization;
using Aw.Context.Core;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The two ways a source's sync ends early without failing the job (#1679): it hit the sync's time cap (SND-CONTEXT-059) or the source's rate limit (AW-CONNECTORS-009). Either is a row with a code, and a job that ended well, so the job's
/// summary is what tells 'partial' from 'done': it leads with 'partial:' and names the source, why it stopped and how many records it fetched. Every other code (a source that is down, a sync already running) keeps its plain 'source CODE'.
/// </summary>
internal static class SourceStops
{
    /// <summary>The connectors' rate-limit code (Aw.Connectors.Core's ConnectorsErrors.RateLimited); this layer does not reference the connectors.</summary>
    internal const string RateLimitedCode = "AW-CONNECTORS-009";

    /// <summary>Whether the code is one of the two early stops.</summary>
    /// <param name="code">A row's code, or null.</param>
    /// <returns>True for the time cap and the rate limit.</returns>
    internal static bool Is(string? code) => Why(code) is not null;

    /// <summary>One source's part of a summary: 'slack stopped at its time cap (SND-CONTEXT-059), 0 fetched' for a stop, 'slack 12 fetched' for a clean row, else 'slack CODE'.</summary>
    /// <param name="source">The source id.</param>
    /// <param name="fetched">How many records the row fetched.</param>
    /// <param name="code">The row's code, or null.</param>
    /// <returns>The part.</returns>
    internal static string Part(string source, int fetched, string? code) => code switch
    {
        null => string.Create(CultureInfo.InvariantCulture, $"{source} {fetched} fetched"),
        _ when Why(code) is { } why => string.Create(CultureInfo.InvariantCulture, $"{source} {why} ({code}), {fetched} fetched"),
        _ => $"{source} {code}",
    };

    private static string? Why(string? code) =>
        string.Equals(code, ContextErrors.SyncCapped.Code, StringComparison.Ordinal) ? "stopped at its time cap"
        : string.Equals(code, RateLimitedCode, StringComparison.Ordinal) ? "stopped by a rate limit"
        : null;
}
