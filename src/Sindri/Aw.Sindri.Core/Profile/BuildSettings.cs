using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.build:' block (line spec §7.2, Appendix D).</summary>
/// <param name="Commands">The commands that check a phase's change (build, test, lint …), each a command line; none until 'line doctor' asks.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Commands = null);
