using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>One repo's 'sandbox.repos.&lt;repo&gt;' declaration (sandbox spec §3.7).</summary>
/// <param name="HomeAllow">More home entries for this repo.</param>
/// <param name="Groups">Host groups this repo's Steps get, by name.</param>
/// <param name="Hosts">Extra hosts for this repo (patterns).</param>
/// <param name="CachePaths">Extra cache paths.</param>
/// <param name="Login">This repo's login source override.</param>
/// <param name="Prepare">The host-side prepare (§3.8).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SandboxRepoSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<HomeAllowEntry>? HomeAllow = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Groups = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Hosts = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? CachePaths = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SandboxRepoLogin? Login = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SandboxPrepare? Prepare = null);
