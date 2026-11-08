using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Graph;

/// <summary>A named database resource.</summary>
/// <param name="Name">Its name.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GraphDatabase(string Name);
