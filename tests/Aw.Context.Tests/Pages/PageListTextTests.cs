using Aw.Context.Core.Pages;

namespace Aw.Context.Tests.Pages;

/// <summary>A pasted page list read into blocks: bullets and numbers, line ends, blank lines, '-&gt;' concepts and stray characters.</summary>
public sealed class PageListTextTests
{
    [Fact, Trait("Lane", "unit")]
    public void Blank_lines_separate_pages_and_the_first_line_is_the_name()
    {
        var blocks = PageListText.Parse("Schedule week\nSubmit\nCancel\n\n  \n\nHome\nOpen");

        Assert.Equal(2, blocks.Count);
        Assert.Equal((1, 1, "Schedule week"), (blocks[0].Block, blocks[0].Line, blocks[0].Name));
        Assert.Equal("Submit|Cancel", string.Join('|', blocks[0].Actions.Select(static a => a.Name)));
        Assert.Equal((2, 7, "Home"), (blocks[1].Block, blocks[1].Line, blocks[1].Name));
        Assert.Equal("Open", string.Join('|', blocks[1].Actions.Select(static a => a.Name)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bullet_or_number_and_the_spaces_after_it_are_dropped()
    {
        var blocks = PageListText.Parse("1. Schedule week\n- Submit\n*  Cancel\n• Approve\n+ Reopen\n12) Close\n1.5 hours");

        Assert.Equal("Schedule week", blocks[0].Name);
        Assert.Equal("Submit|Cancel|Approve|Reopen|Close|1.5 hours", string.Join('|', blocks[0].Actions.Select(static a => a.Name)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Windows_and_old_mac_line_ends_read_as_new_lines()
    {
        var blocks = PageListText.Parse("Home\r\nOpen\r\rSettings\rSave");

        Assert.Equal("Home|Settings", string.Join('|', blocks.Select(static b => b.Name)));
        Assert.Equal([1, 4], blocks.Select(static b => b.Line));
        Assert.Equal("Save", string.Join('|', blocks[1].Actions.Select(static a => a.Name)));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_arrow_splits_the_action_on_its_first_arrow_into_comma_separated_concepts()
    {
        var action = PageListText.Parse("Home\nSubmit -> Approval workflow,  Self-scheduling ,, Billing")[0].Actions[0];

        Assert.Equal("Submit", action.Name);
        Assert.Equal(["Approval workflow", "Self-scheduling", "Billing"], action.Concepts);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_arrow_with_nothing_after_it_means_no_concepts()
    {
        var actions = PageListText.Parse("Home\nSubmit ->\nCancel -> a -> b")[0].Actions;

        Assert.Equal(("Submit", 0), (actions[0].Name, actions[0].Concepts.Count));
        Assert.Equal(["a -> b"], actions[1].Concepts);
    }

    [Fact, Trait("Lane", "unit")]
    public void Control_and_format_characters_are_stripped()
    {
        var blocks = PageListText.Parse("Sched\u0000ule​ week\nSub\u0007mit");

        Assert.Equal("Schedule week", blocks[0].Name);
        Assert.Equal("Submit", string.Join('|', blocks[0].Actions.Select(static a => a.Name)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Nothing_to_read_gives_no_blocks()
    {
        Assert.Empty(PageListText.Parse(string.Empty));
        Assert.Empty(PageListText.Parse("\n \n\t\n-\n"));
    }
}
