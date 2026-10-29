using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.sources' as written (spec §8.8): the external sources the profile names.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SourcesSection(LinearSection? Linear = null, GitHubSection? Github = null, SlackSection? Slack = null);
