using System.Text.Json.Serialization;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'sandbox:' section (sandbox spec §9). Left out of the canonical JSON when absent, so a profile without it keeps its approved hash and gets <see cref="SandboxDefaults"/>.</summary>
/// <param name="Limits">Limits (memory, cpu, tasks, sessions, total, wall grace, home tmpfs, park max).</param>
/// <param name="Fs">Filesystem additions: home entries bound in and extra denied paths.</param>
/// <param name="Network">Host groups and the network mode.</param>
/// <param name="Repos">Per-repo declarations by repo name (sandbox spec §3.7).</param>
/// <param name="Inner">The agent CLI's own sandbox: auto, on or off.</param>
/// <param name="Credentials">GitHub credentials for Steps.</param>
/// <param name="Login">The model credential source (SB18).</param>
/// <param name="Attended">Settings for sessions the owner opens (§3.9).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SandboxSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SandboxLimits? Limits = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SandboxFs? Fs = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SandboxNetwork? Network = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, SandboxRepoSettings>? Repos = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] InnerSandbox? Inner = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SandboxCredentials? Credentials = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SandboxLogin? Login = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SandboxAttended? Attended = null);
