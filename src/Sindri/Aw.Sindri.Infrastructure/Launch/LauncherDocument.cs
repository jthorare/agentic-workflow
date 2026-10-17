using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The shape of launcher.json and builtins.json (schemas/launcher.schema.json): unknown keys are errors, in every section.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record LauncherDocument(int Schema)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ViewerSection? Viewer { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HostSection? Host { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, AgentSection>? Agents { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MetaSection? Meta { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SessionsSection? Sessions { get; init; }
}
