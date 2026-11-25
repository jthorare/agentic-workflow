using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #4359: a Wait has no required field, but a wait needs exactly one of "Wait for" and "Wait until". The blocks list serves that as "oneOf"; Save stays off, with a line, until it holds.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class AddStepOneOfTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> AddedWait()
    {
        _f.Server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.GraphEdit.BeginAdd(Token);
        var block = page.GraphEdit.Groups.SelectMany(static g => g.Blocks).Single(b => string.Equals(b.Type, "wait", StringComparison.Ordinal));
        await page.GraphEdit.AddStep(block, Token);
        return page;
    }

    private static ConfigFieldViewModel Field(WorkflowsPageViewModel page, string name) =>
        page.Editor.Fields.Single(f => string.Equals(f.Name, name, StringComparison.Ordinal));

    [Fact]
    public async Task A_new_Wait_opens_with_Save_off_and_a_line_on_Wait_for()
    {
        var page = await AddedWait();

        Assert.False(page.CanSave);
        Assert.Equal("Fill in Wait for or Wait until to save.", Field(page, "duration").NeedText);
        Assert.Equal(string.Empty, Field(page, "until").NeedText);
        Assert.Equal(string.Empty, Field(page, "timezone").NeedText);
    }

    [Fact]
    public async Task Both_filled_keeps_Save_off_and_says_to_fill_only_one()
    {
        var page = await AddedWait();

        Field(page, "duration").Text = "15m";
        Field(page, "until").Text = "07:00";

        Assert.False(page.CanSave);
        Assert.Equal("Fill in only one of Wait for and Wait until.", Field(page, "duration").NeedText);
    }

    [Theory]
    [InlineData("duration", "15m")]
    [InlineData("until", "07:00")]
    public async Task Exactly_one_filled_lights_Save_with_no_line(string name, string text)
    {
        var page = await AddedWait();

        Field(page, name).Text = text;

        Assert.True(page.CanSave);
        Assert.All(page.Editor.Fields, static f => Assert.False(f.HasNeed));
    }

    [Fact]
    public async Task Clearing_the_one_filled_field_turns_Save_off_again()
    {
        var page = await AddedWait();
        Field(page, "until").Text = "07:00";

        Field(page, "until").Text = string.Empty;

        Assert.False(page.CanSave);
        Assert.Equal("Fill in Wait for or Wait until to save.", Field(page, "duration").NeedText);
    }
}
