using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Tests.Todos;

/// <summary>A todo's text rules (todo spec D3, §6): one line, 1 to 280 characters, no Control or Format character, no note marker, no markdown link.</summary>
public sealed class TodoTextTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("reply to Sam about the offsite")]
    [InlineData("  trim me  ")]
    [InlineData("call 📞 the dentist")]   // an emoji that is not one of the markers
    [InlineData("see docs [here] later")]   // brackets without a link target
    public void Good_text_passes_trimmed(string text)
    {
        var checkedText = TodoText.Check(text);

        Assert.True(checkedText.IsSuccess, checkedText.IsError ? checkedText.Error.ToString() : null);
        Assert.Equal(text.Trim(), checkedText.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void Exactly_280_characters_pass_and_281_are_SND_TODO_001()
    {
        Assert.True(TodoText.Check(new string('a', TodoText.MaxLength)).IsSuccess);

        var over = TodoText.Check(new string('a', TodoText.MaxLength + 1));

        Assert.True(over.IsError);
        Assert.Equal("SND-TODO-001", over.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_text_is_SND_TODO_001(string text)
    {
        var empty = TodoText.Check(text);

        Assert.True(empty.IsError);
        Assert.Equal("SND-TODO-001", empty.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("two\nlines")]
    [InlineData("tab\there")]
    [InlineData("bell\u0007")]
    [InlineData("zero\u200Bwidth")]   // Format: zero width space
    [InlineData("bidi\u202Eoverride")]   // Format: right-to-left override
    [InlineData("tag\U000E0041char")]   // Format: a tag character
    [InlineData("line\u2028separator")]
    [InlineData("paragraph\u2029separator")]
    public void Control_Format_and_separator_characters_are_SND_TODO_007(string text)
    {
        var refused = TodoText.Check(text);

        Assert.True(refused.IsError);
        Assert.Equal("SND-TODO-007", refused.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("pay rent 📅 2026-12-01")]
    [InlineData("renew ✅ the cert")]
    [InlineData("sneaky ^td-01jc5a8xq0n4w7ms2t9r3vbk6e")]
    [InlineData("read [the plan](https://example.com/plan)")]
    [InlineData("read [the plan](sindri://linear/ENG-12)")]
    public void A_note_marker_or_a_markdown_link_is_SND_TODO_007(string text)
    {
        var refused = TodoText.Check(text);

        Assert.True(refused.IsError);
        Assert.Equal("SND-TODO-007", refused.Error.Code);
    }
}
