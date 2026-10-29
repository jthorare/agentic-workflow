using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.sources' as written (spec §8.8): the external sources the profile names. 'onyx' is retired: read so an old profile loads, never used.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SourcesSection(System.Text.Json.JsonElement? Onyx = null, LinearSection? Linear = null, GitHubSection? Github = null, SlackSection? Slack = null);
