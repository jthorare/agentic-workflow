using Microsoft.Extensions.Logging;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>What every linear source shares: the HTTP client, the environment that resolves the key's pointer, the clock and the log.</summary>
/// <param name="Http">The client (production: <see cref="LinearHttp.Client"/>, no proxy and no redirects).</param>
/// <param name="Environment">The environment Composition passes; called with the settings' pointer text, it returns the key (Sindri resolves pointers, spec §4); each source's client calls it once.</param>
/// <param name="Clock">The clock (a hit's AsOf).</param>
/// <param name="Logger">The log; it gets operations, statuses and codes, never the key.</param>
internal sealed record LinearSourceParts(HttpClient Http, Func<string, string?> Environment, TimeProvider Clock, ILogger<LinearClient> Logger)
{
    /// <summary>How long Linear gets to answer each fetch, versions and probe call (a search uses the query's own deadline); 4 s.</summary>
    public TimeSpan CallDeadline { get; init; } = TimeSpan.FromSeconds(15);
}
