using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>A repo's 'workflow:' block: what overrides the box-wide 'workflow:' settings for this repo.</summary>
/// <param name="Judge">The repo's 'judge:' section; its provider list wins over the box's.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RepoWorkflowSettings([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JudgeSettings? Judge = null);
