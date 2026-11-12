using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Tests.Controls;

/// <summary>A row's glyph comes from its kind and state class (#2978); the API's state key only breaks ties the class cannot.</summary>
[Lane(TestLane.Unit)]
public sealed class RowGlyphsTests
{
    [Theory]
    [InlineData("pr", "Active", "PrOpen")]
    [InlineData("pr", "Done", "Merged")]
    [InlineData("run", "Failed", "Failed")]
    [InlineData("thread", "Waiting", "Message")]
    [InlineData("run", "Active", "AgentRunning")]
    public void Kind_and_state_class_pick_the_glyph(string kind, string state, string expected) =>
        Assert.Equal(Enum.Parse<RowGlyph>(expected), RowGlyphs.For(kind, Enum.Parse<RowStateClass>(state)));

    [Fact]
    public void A_draft_pr_needs_the_state_key() =>
        Assert.Equal(RowGlyph.PrDraft, RowGlyphs.For("pr", RowStateClass.Open, stateKey: "draft"));
}
