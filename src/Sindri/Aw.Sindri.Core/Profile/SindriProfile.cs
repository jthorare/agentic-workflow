using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The Sindri profile, schema version 1 (design spec §11.1, owner decision 2026-10-11: YAML, read through AwJson).
/// Unknown keys are errors; new optional keys join version 1, renames and removals bump it.
/// </summary>
/// <param name="SchemaVersion">The format version; this build reads 1.</param>
/// <param name="User">Who runs this Sindri (claim refs and the event actor's owner).</param>
/// <param name="Tenant">The tenant key every row carries (factory addendum §17.20); one value until team mode.</param>
/// <param name="Repos">The repos in the path.</param>
/// <param name="Services">Background services by name, each with its cadence.</param>
/// <param name="Api">The local API's settings.</param>
/// <param name="Workflow">
/// The workflow engine's settings (engine spec S1-D11). Left out of the canonical JSON when absent, so a profile without
/// a 'workflow:' block keeps the hash it was approved under.
/// </param>
/// <param name="Index">The 'index:' section (docs/sindri/profile.md): deny paths for every repo. Left out of the canonical JSON when absent, like Context.</param>
/// <param name="Context">
/// The context engine's section ('context:', context spec §8.8). Sindri can't type it (the engine owns its keys), so it
/// keeps the JSON and the engine checks it (IContextEngine.CheckSettings) at validate and approve. Left out of the
/// canonical JSON when absent, so a profile without it keeps its hash (and its approval).
/// </param>
/// <param name="Launch">The 'launch:' block (UI spec E-4). Left out of the canonical JSON when absent, so a profile without it keeps its hash.</param>
/// <param name="Agents">The 'agents:' block (budgets, SL8's stepArgvHash). Left out of the canonical JSON when absent.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SindriProfile(
    int SchemaVersion,
    string User,
    string Tenant = SindriProfile.DefaultTenant,
    IReadOnlyList<RepoEntry>? Repos = null,
    IReadOnlyDictionary<string, ServiceSettings>? Services = null,
    ApiSettings? Api = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WorkflowSettings? Workflow = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? Context = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IndexSettings? Index = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LaunchSettings? Launch = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AgentSettings? Agents = null)
{
    /// <summary>The tenant key until team mode.</summary>
    public const string DefaultTenant = "local";

    /// <summary>The schema version this build reads.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>An empty services map, for a profile that names none.</summary>
    public static IReadOnlyDictionary<string, ServiceSettings> NoServices { get; } = new Dictionary<string, ServiceSettings>(StringComparer.Ordinal);
}
