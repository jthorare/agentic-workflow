namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>What the host tells the pure builder (Core reads no environment): the Host or the adapter fills it from the box.</summary>
/// <param name="Home">The owner's home directory.</param>
/// <param name="StateDir">AW_STATE_DIR (default ~/.agentic-workflow).</param>
/// <param name="SindriHome">The real AW_SINDRI_HOME as the control plane has it (default $AW_STATE_DIR/sindri-app); a launch sees its session home instead (spec §3.1, rev 7).</param>
/// <param name="CsBinDir">The installed C# binaries' stamp directory, $AW_STATE_DIR/bin/cs/&lt;stamp&gt;, with bin/cs/current already resolved by the host (Core touches no file system); empty when none is installed.</param>
/// <param name="Uid">The owner's user id.</param>
/// <param name="TotalMemoryBytes">The box's RAM.</param>
/// <param name="CpuCores">The number of cores.</param>
/// <param name="IsMacOs">True on macOS (adds the keychain deny).</param>
/// <param name="Environment">The host environment the allowlist copies from (PATH, LANG, TERM, ...); other names are never read.</param>
/// <param name="OtherHomes">Other users' home directories to deny, found by the adapter.</param>
/// <param name="InstalledHooks">The owner hooks installed for the target agent (their 'aw:' tags), read by the adapter; null means the inventory itself.</param>
public sealed record SandboxHostFacts(
    string Home,
    string StateDir,
    string SindriHome,
    string CsBinDir,
    int Uid,
    long TotalMemoryBytes,
    int CpuCores,
    bool IsMacOs,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<string> OtherHomes,
    IReadOnlyList<string>? InstalledHooks = null);
