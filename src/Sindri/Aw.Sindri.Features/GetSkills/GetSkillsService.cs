using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetSkills;

/// <summary>Lists the skill blocks serve loaded at start, with the path each came from and the reason for any that didn't load (skill flows spec §7).</summary>
internal sealed class GetSkillsService : IService<GetSkillsRequest, GetSkillsResponse>
{
    public ValueTask<Result<GetSkillsResponse>> Handle(GetSkillsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
