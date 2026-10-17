using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.judge:' block (DT-J plan Q8): which providers a Judge Step may ask.</summary>
/// <param name="Providers">Provider names ('jev', 'claude-cli', 'codex-cli', 'cursor-cli', 'rules'); absent: <see cref="JudgeProviders.Default"/>. 'jev' runs only when listed here.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record JudgeSettings([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Providers = null);
