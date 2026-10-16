namespace Aw.Sindri.Infrastructure;

/// <summary>Sindri's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="Home">Sindri's state directory (0700): the ledger, approvals, the endpoint record and logs.</param>
/// <param name="ProfilePath">The live profile file.</param>
internal sealed record SindriSettings(string Home, string ProfilePath)
{
    /// <summary>The ledger database.</summary>
    public string LedgerPath => Path.Combine(Home, "ledger.db");

    /// <summary>The desk's notices, one JSON line each (engine spec §6.3).</summary>
    public string NotificationsPath => Path.Combine(Home, "notifications.jsonl");

    /// <summary>The daily digests of non-interrupting notices, '&lt;yyyy-MM-dd&gt;.jsonl' (engine spec §6.3).</summary>
    public string DigestDirectory => Path.Combine(Home, "digest");
    /// <summary>The views database (derived from the ledger; deleting it rebuilds it).</summary>
    public string ViewsPath => Path.Combine(Home, "views.db");

    /// <summary>Approved profile snapshots, one directory per hash.</summary>
    public string ApprovalsDirectory => Path.Combine(Home, "approved");

    /// <summary>The control plane's endpoint record.</summary>
    public string EndpointPath => Path.Combine(Home, "control.json");

    /// <summary>The per-start human token (0600, in an 'auth' folder that is 0700).</summary>
    public string TokenPath => Path.Combine(Home, "auth", "human.token");

    /// <summary>The temp directory ($TMPDIR, else the platform's), for the prover rule.</summary>
    public string TempDirectory { get; init; } = Path.GetTempPath();

    /// <summary>The state root ($AW_STATE_DIR, else ~/.agentic-workflow), whose '&lt;slug&gt;/proof/' folders the prover rule accepts.</summary>
    public string StateDirectory { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow");

    /// <summary>Whether this home honours X-Sindri-Client: prover (API spec §6, plan G4): it lies inside the temp directory or a proof folder.</summary>
    public bool ProverLabelAllowed =>
        RealPath.Of(Home) is { } home && RealPath.Of(TempDirectory) is { } temp && RealPath.Of(StateDirectory) is { } state
        && ProverHome.Allows(home, temp, state);
}
