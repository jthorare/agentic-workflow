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
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SindriProfile(
    int SchemaVersion,
    string User,
    string Tenant = SindriProfile.DefaultTenant,
    IReadOnlyList<RepoEntry>? Repos = null,
    IReadOnlyDictionary<string, ServiceSettings>? Services = null,
    ApiSettings? Api = null)
{
    /// <summary>The tenant key until team mode.</summary>
    public const string DefaultTenant = "local";

    /// <summary>The schema version this build reads.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>An empty services map, for a profile that names none.</summary>
    public static IReadOnlyDictionary<string, ServiceSettings> NoServices { get; } = new Dictionary<string, ServiceSettings>(StringComparer.Ordinal);
}
