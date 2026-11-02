using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>One 'context.sources.slack.channels' entry as written.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SlackChannelSection(string? Id = null, string? Name = null);
