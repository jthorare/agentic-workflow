using Aw.Sindri.Core.Skills;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What a 'launch.owner-waiting' event says (skill flows spec §7).</summary>
/// <param name="Reason">Why the owner is needed.</param>
/// <param name="Text">The skill's question, already redacted; null for every other reason.</param>
/// <param name="ResultPath">The result file the reader looked at.</param>
/// <param name="Detail">The parse error or the other skill's name, redacted; null otherwise.</param>
/// <param name="TurnEnded">When the turn ended.</param>
internal sealed record OwnerWaitingRow(SkillWaitReason Reason, string? Text, string ResultPath, string? Detail, DateTimeOffset TurnEnded);
