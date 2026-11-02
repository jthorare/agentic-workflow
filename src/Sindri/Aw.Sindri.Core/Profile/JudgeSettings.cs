using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.judge:' block (DT-J plan Q8): which providers a Judge Step may ask, and how many calls it runs at once.</summary>
/// <param name="Providers">Provider names ('jev', 'claude-cli', 'codex-cli', 'cursor-cli', 'rules'); absent: <see cref="JudgeProviders.Default"/>. 'jev' runs only when listed here.</param>
/// <param name="Parallel">How many judge calls a judge Step runs at once, 1 to 16 (issue 1440); absent: the Step's own default.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record JudgeSettings([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Providers = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Parallel = null);
