using Aw.Judge.Core.Ids;
using Aw.Judge.Features.MapBriefByName;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.MapBriefByName;

public sealed class MapBriefByNameTests
{
    private readonly InMemoryBriefStore _briefs = new();

    [Fact, Trait("Lane", "unit")]
    public async Task MapBriefByName_returns_the_oldest_unmapped_brief()
    {
        _ = await _briefs.Save(A.Brief().WithToolUseId("toolu_new").Named("helper").SavedAt(KnownInstants.Epoch.AddSeconds(2)).Build(), CancellationToken.None);
        _ = await _briefs.Save(A.Brief().WithToolUseId("toolu_old").Named("helper").SavedAt(KnownInstants.Epoch.AddSeconds(1)).Build(), CancellationToken.None);
        _ = await _briefs.Save(A.Brief().WithToolUseId("toolu_other").Named("other").Build(), CancellationToken.None);

        var result = await new MapBriefByNameService(_briefs).Handle(A.MapBriefByNameRequest().Named("helper").Build(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ToolUseId.From("toolu_old"), result.Value.ToolUseId);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task MapBriefByName_returns_no_tool_use_id_when_none_is_unmapped()
    {
        var result = await new MapBriefByNameService(_briefs).Handle(A.MapBriefByNameRequest().Build(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ToolUseId);
    }
}
