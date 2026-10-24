using Microsoft.Extensions.Logging;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>What every onyx source shares: the HTTP client, the environment that resolves the key's pointer, the clock and the log.</summary>
/// <param name="Http">The client (production: <see cref="OnyxHttp.Client"/>, no proxy and no redirects).</param>
/// <param name="Environment">The environment Composition passes; called with the settings' pointer text, it returns the key (Sindri resolves pointers, spec §4); each source's client calls it once.</param>
/// <param name="Clock">The clock (a hit's AsOf).</param>
/// <param name="Logger">The log; it gets routes, statuses and codes, never the key.</param>
internal sealed record OnyxSourceParts(HttpClient Http, Func<string, string?> Environment, TimeProvider Clock, ILogger<OnyxClient> Logger)
{
    /// <summary>How long Onyx gets to answer each fetch and probe call (a search uses the query's own deadline); 4 s.</summary>
    public TimeSpan CallDeadline { get; init; } = TimeSpan.FromSeconds(15);
}
