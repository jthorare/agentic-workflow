namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Where the adapter keeps its files under the Sindri home: the facts a start or a desk report stored, the nonces, and
/// the event bridge's cursor.
/// </summary>
/// <param name="Home">The Sindri home, as <see cref="TemporalSettings"/> reads it.</param>
internal sealed record WorkflowsHome(string Home)
{
    /// <summary>Facts by SHA-256, stored 0600 before the start or the completion that references them.</summary>
    public string FactsDirectory => Path.Combine(Home, "workflow", "facts");

    /// <summary>The API's nonce table, one owner-only file per nonce (engine P4 unit (c), until API P1's table).</summary>
    public string NoncesDirectory => Path.Combine(Home, "workflow", "nonces");

    /// <summary>The event bridge's cursor: the last ledger sequence it delivered (engine P4 unit (d)).</summary>
    public string EventsCursorFile => Path.Combine(Home, "workflow", "events.cursor");

    /// <summary>The router stub's cursor: the last ledger sequence it handled (engine P7 plan D8).</summary>
    public string RouterCursorFile => Path.Combine(Home, "workflow", "router.cursor");

    /// <summary>The 'pr-watch' poller's state: the last digest of each watched pull request and each host's back-off (PR babysitter spec §6.2).</summary>
    public string PrWatchFile => Path.Combine(Home, "workflow", "pr-watch.json");
}
