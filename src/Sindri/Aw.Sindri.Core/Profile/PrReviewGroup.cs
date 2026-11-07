using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>A named group of files.</summary>
/// <param name="Name">The group's heading.</param>
/// <param name="Globs">The globs that put a file in it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrReviewGroup(string Name, IReadOnlyList<string> Globs);
