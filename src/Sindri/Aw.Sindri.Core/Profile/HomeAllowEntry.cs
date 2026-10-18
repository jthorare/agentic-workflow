using System.Text.Json.Serialization;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Core.Profile;

/// <summary>One entry of 'homeAllow': a $HOME path bound back into the sandbox.</summary>
/// <param name="Path">A path relative to $HOME.</param>
/// <param name="Mode">'ro' or 'rw'; read-only when left out.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HomeAllowEntry(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Path = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MountMode? Mode = null);
