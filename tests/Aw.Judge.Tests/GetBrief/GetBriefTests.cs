using Aw.Judge.Core.Errors;
using Aw.Judge.Features.GetBrief;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.GetBrief;

public sealed class GetBriefTests
{
    private readonly InMemoryBriefStore _briefs = new();

    [Fact, Trait("Lane", "unit")]
    public async Task GetBrief_returns_the_brief_a_tool_call_saved()
    {
        var brief = A.Brief().Named("helper").Build();
        _ = await _briefs.Save(brief, CancellationToken.None);
        _ = await _briefs.Save(A.Brief().WithToolUseId("toolu_2").Build(), CancellationToken.None);

        var result = await new GetBriefService(_briefs).Handle(A.GetBriefRequest().Build(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(brief, result.Value.Brief);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetBrief_returns_NoBrief_when_none_was_saved()
    {
        var result = await new GetBriefService(_briefs).Handle(A.GetBriefRequest().Build(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(JudgeErrors.NoBrief.Code, result.Error.Code);
    }
}
