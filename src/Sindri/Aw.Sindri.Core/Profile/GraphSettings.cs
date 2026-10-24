using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The 'index.graph' keys (code index spec §13).</summary>
/// <param name="Enabled">Off leaves the graph layer disabled (default true).</param>
/// <param name="MaxMB">The largest graph file read (default <see cref="DefaultMaxMB"/>).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GraphSettings(bool? Enabled = null, int? MaxMB = null)
{
    /// <summary>The default for 'maxMB'.</summary>
    public const int DefaultMaxMB = 1_024;
}
