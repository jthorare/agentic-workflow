namespace Aw.Sindri.Core.Sandbox;

/// <summary>
/// The only environment variables the sandbox reads: paths a test redirects (sandbox spec §9). No variable switches the sandbox off.
/// They are read by the adapters (SB-2 bubblewrap, SB-3a systemd-run, SB-4a socat), so '.env.example' marks them indirect until then.
/// </summary>
public static class SandboxEnvironment
{
    /// <summary>The bubblewrap binary.</summary>
    public const string Bwrap = "AW_SANDBOX_BWRAP";

    /// <summary>The systemd-run binary.</summary>
    public const string SystemdRun = "AW_SANDBOX_SYSTEMD_RUN";

    /// <summary>The socat binary.</summary>
    public const string Socat = "AW_SANDBOX_SOCAT";
}
