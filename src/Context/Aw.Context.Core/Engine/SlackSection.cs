using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.sources.slack' as written (owned index spec §5.3, Q7): the channels read, its own retention, the secret pointer to the bot token and the workspace name its permalinks are built from.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SlackSection(IReadOnlyList<SlackChannelSection?>? Channels = null, int? RetainDays = null, string? Token = null, string? Workspace = null);
