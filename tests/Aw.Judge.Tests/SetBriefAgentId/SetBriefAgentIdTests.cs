using Aw.Judge.Core.Ids;
using Aw.Judge.Features.SetBriefAgentId;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.SetBriefAgentId;

public sealed class SetBriefAgentIdTests
{
    private readonly InMemoryBriefStore _briefs = new();

    [Fact, Trait("Lane", "unit")]
    public async Task SetBriefAgentId_maps_the_brief_to_the_agent()
    {
        _ = await _briefs.Save(A.Brief().Build(), CancellationToken.None);

        var result = await new SetBriefAgentIdService(_briefs).Handle(A.SetBriefAgentIdRequest().Build(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AgentId.From("agentid-1"), Assert.Single(_briefs.Briefs).AgentId);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SetBriefAgentId_on_an_unknown_tool_use_id_changes_nothing()
    {
        _ = await _briefs.Save(A.Brief().WithToolUseId("toolu_other").Build(), CancellationToken.None);

        var result = await new SetBriefAgentIdService(_briefs).Handle(A.SetBriefAgentIdRequest().Build(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(Assert.Single(_briefs.Briefs).AgentId);
    }
}
