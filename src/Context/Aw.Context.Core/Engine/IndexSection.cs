using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.index' as written (owned index spec §5.5, §5.6, §6, §8, Q7): the window, the chunk ceiling, retention, the dense floors and the Onyx leftover acknowledgement.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record IndexSection(int? WindowDays = null, int? MaxChunks = null, int? RetainDays = null, FloorsSection? Floors = null, bool? AcknowledgeOnyxLeftover = null);
