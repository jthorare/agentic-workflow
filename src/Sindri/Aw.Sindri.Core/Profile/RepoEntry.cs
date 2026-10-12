using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>A repo in the path.</summary>
/// <param name="Name">Its short name.</param>
/// <param name="Path">The absolute path of its checkout.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RepoEntry(string Name, string Path);
