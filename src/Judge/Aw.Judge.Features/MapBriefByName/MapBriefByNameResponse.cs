using Aw.Judge.Core.Ids;

namespace Aw.Judge.Features.MapBriefByName;

/// <summary>The MapBriefByName response.</summary>
/// <param name="ToolUseId">The oldest unmapped brief's tool use id under that name, or null when there is none.</param>
public sealed record MapBriefByNameResponse(ToolUseId? ToolUseId);
