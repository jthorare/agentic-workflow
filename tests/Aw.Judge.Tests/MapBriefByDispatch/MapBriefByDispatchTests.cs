using Aw.Judge.Core.Ids;
using Aw.Judge.Features.MapBriefByDispatch;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.MapBriefByDispatch;

public sealed class MapBriefByDispatchTests
{
    private readonly InMemoryBriefStore _briefs = new();

    [Fact, Trait("Lane", "unit")]
    public async Task MapBriefByDispatch_returns_the_oldest_unmapped_brief_of_the_dispatch()
    {
        _ = await _briefs.Save(A.Brief().WithToolUseId("toolu_new").SavedAt(KnownInstants.Epoch.AddSeconds(2)).Build(), CancellationToken.None);
        _ = await _briefs.Save(A.Brief().WithToolUseId("toolu_old").SavedAt(KnownInstants.Epoch.AddSeconds(1)).Build(), CancellationToken.None);
        _ = await _briefs.Save(A.Brief().WithToolUseId("toolu_other").DispatchedBy("session-1", "prompt-2", "general-purpose").Build(), CancellationToken.None);

        var result = await new MapBriefByDispatchService(_briefs).Handle(A.MapBriefByDispatchRequest().Build(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ToolUseId.From("toolu_old"), result.Value.ToolUseId);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task MapBriefByDispatch_returns_no_tool_use_id_for_another_subagent_type()
    {
        _ = await _briefs.Save(A.Brief().Build(), CancellationToken.None);

        var result = await new MapBriefByDispatchService(_briefs).Handle(A.MapBriefByDispatchRequest().OfType("Explore").Build(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ToolUseId);
    }
}
