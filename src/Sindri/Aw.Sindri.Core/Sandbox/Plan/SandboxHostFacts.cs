namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>What the host tells the pure builder (Core reads no environment): the Host or the adapter fills it from the box.</summary>
/// <param name="Home">The owner's home directory.</param>
/// <param name="StateDir">AW_STATE_DIR (default ~/.agentic-workflow).</param>
/// <param name="SindriHome">AW_SINDRI_HOME (default $AW_STATE_DIR/sindri-app).</param>
/// <param name="ToolkitDir">The toolkit checkout the judge wrapper points at.</param>
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
    string ToolkitDir,
    int Uid,
    long TotalMemoryBytes,
    int CpuCores,
    bool IsMacOs,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<string> OtherHomes,
    IReadOnlyList<string>? InstalledHooks = null);
