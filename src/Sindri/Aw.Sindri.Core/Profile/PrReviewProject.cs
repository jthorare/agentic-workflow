using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>A project's repos and the named settings their PRs get (spec §7.2).</summary>
/// <param name="Project">A 'prj_…' id or a 'linear:project/&lt;id&gt;' key.</param>
/// <param name="Repos">The repos, as owner/name.</param>
/// <param name="Settings">The named settings entry; absent means 'default'.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrReviewProject(
    string Project,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Repos = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Settings = null);
