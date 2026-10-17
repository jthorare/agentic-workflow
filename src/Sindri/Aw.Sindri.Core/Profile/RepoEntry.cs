using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>A repo in the path.</summary>
/// <param name="Name">Its short name.</param>
/// <param name="Path">The absolute path of its checkout.</param>
/// <param name="Index">This repo's 'index:' section (its deny paths join the profile's), or null.</param>
/// <param name="Workflow">This repo's 'workflow:' section (its judge providers win over the box's), or null.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RepoEntry(string Name, string Path, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IndexSettings? Index = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RepoWorkflowSettings? Workflow = null);
