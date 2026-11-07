using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetSkills;

/// <summary>Reads the loaded skill blocks (GET /sindri/skills; skill flows spec §7): cheap, it reads what serve loaded at start.</summary>
internal sealed record GetSkillsRequest : IQuery<GetSkillsResponse>;
