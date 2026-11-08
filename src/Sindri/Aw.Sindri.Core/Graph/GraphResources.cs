using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Graph;

/// <summary>A workflow's resources ('resources' in the spec): what its blocks may use besides their own config.</summary>
/// <param name="Database">A named database the workflow keeps its state in, or null.</param>
/// <param name="Sandbox">The default size of a 'run_agent' sandbox, or null.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GraphResources(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GraphDatabase? Database = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GraphSandbox? Sandbox = null);
