using Aw.Judge.Core.Ids;

namespace Aw.Judge.Features.MapBriefByDispatch;

/// <summary>The MapBriefByDispatch response.</summary>
/// <param name="ToolUseId">The oldest unmapped brief's tool use id for that dispatch, or null when there is none.</param>
public sealed record MapBriefByDispatchResponse(ToolUseId? ToolUseId);
