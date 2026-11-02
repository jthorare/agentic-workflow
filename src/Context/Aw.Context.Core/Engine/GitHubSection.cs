using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.sources.github' as written (owned index spec §5.3): the repos, an Enterprise API host, whether a private host is allowed, and the secret pointer to the token.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record GitHubSection(IReadOnlyList<string>? Repos = null, string? Host = null, bool? AllowPrivateHost = null, string? Token = null);
