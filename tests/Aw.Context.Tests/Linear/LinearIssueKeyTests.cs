using Aw.Context.Core.Linear;

namespace Aw.Context.Tests.Linear;

[Trait("Lane", "unit")]
public sealed class LinearIssueKeyTests
{
    [Fact]
    public void A_key_is_read_as_typed()
    {
        var key = LinearIssueKey.Parse("ENG-12");

        Assert.True(key.IsSuccess);
        Assert.Equal("ENG", key.Value.Team);
        Assert.Equal(12, key.Value.Number);
    }

    [Fact]
    public void A_lower_case_key_is_the_upper_case_one()
    {
        var key = LinearIssueKey.Parse("eng-12");

        Assert.True(key.IsSuccess, key.IsError ? key.Error.Message : "");
        Assert.Equal("ENG-12", key.Value.ToString());
        Assert.Equal(LinearIssueKey.Parse("ENG-12").Value, key.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ENG12")]
    [InlineData("ENG-0")]
    [InlineData("ENG-012")]
    [InlineData("1ENG-1")]
    [InlineData("ENG-1 ")]
    public void Text_that_is_not_a_key_is_refused_with_the_text_as_typed(string text)
    {
        var key = LinearIssueKey.Parse(text);

        Assert.True(key.IsError);
        Assert.Equal("SND-CONTEXT-006", key.Error.Code);
        Assert.Contains($"'{text}'", key.Error.Message, StringComparison.Ordinal);
    }
}
