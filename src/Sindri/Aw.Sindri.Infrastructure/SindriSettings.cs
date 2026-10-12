namespace Aw.Sindri.Infrastructure;

/// <summary>Sindri's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="Home">Sindri's state directory (0700): the ledger, approvals, the endpoint record and logs.</param>
/// <param name="ProfilePath">The live profile file.</param>
internal sealed record SindriSettings(string Home, string ProfilePath)
{
    /// <summary>The ledger database.</summary>
    public string LedgerPath => Path.Combine(Home, "ledger.db");

    /// <summary>Approved profile snapshots, one directory per hash.</summary>
    public string ApprovalsDirectory => Path.Combine(Home, "approved");

    /// <summary>The control plane's endpoint record.</summary>
    public string EndpointPath => Path.Combine(Home, "control.json");
}
