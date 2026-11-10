using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Tests.Toolkits;

public sealed class ToolNamesTests
{
    [Fact, Trait("Lane", "unit")]
    public void Names_use_one_separator_function_and_stay_within_the_client_limit()
    {
        var name = ToolNames.For("github-triage", "list_issues").Value;

        Assert.Equal("github-triage__list_issues", name);
        Assert.DoesNotContain('.', name);
        Assert.True(ToolNames.TryParse(name, out var toolkit, out var tool));
        Assert.Equal(("github-triage", "list_issues"), (toolkit, tool));

        var tooLong = ToolNames.For(new string('a', 40), new string('b', 30));
        Assert.True(tooLong.IsError);
        Assert.Equal(SindriErrors.ToolkitNameInvalid.Code, tooLong.Error.Code);
        Assert.True(ToolNames.For(new string('a', 31), new string('b', 31)).Value.Length == ToolNames.MaxLength);
        Assert.True(ToolNames.For("a.b", "x").IsError);
        Assert.True(ToolNames.For("a", "X").IsError);
        Assert.False(ToolNames.TryParse("plain_tool", out _, out _));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_tool_name_with_a_double_underscore_splits_at_the_first_separator()
    {
        Assert.True(ToolNames.TryParse("kit__a__b", out var toolkit, out var tool));
        Assert.Equal(("kit", "a__b"), (toolkit, tool));
    }
}
