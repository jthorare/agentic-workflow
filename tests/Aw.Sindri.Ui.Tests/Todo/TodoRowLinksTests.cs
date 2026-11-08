using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;
using static Aw.Sindri.Ui.Tests.Todo.TodoFixture;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>O on a todo row (todo spec §3.3): one link opens at once, several open a list with J, K, Enter and Esc.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoRowLinksTests
{
    private readonly TodoFixture _f = new();
    private readonly List<string> _opened = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The golden's "rotate the staging key" has two links; this one gets a third.
    private static JsonNode Three()
    {
        var todo = Of(2);
        todo["links"]!.AsArray().Add(JsonNode.Parse("""{"kind":"item","ref":"ITM-0000000000000000000000000000000f","href":"sindri://item/ITM-0000000000000000000000000000000f"}"""));
        return todo;
    }

    private async Task<TodoPanelViewModel> Serving(params JsonNode[] todos)
    {
        _f.ServeTodos(todos);
        var panel = await _f.Read(Token);
        panel.LinkOpened += (_, e) => _opened.Add(e.Href);
        return panel;
    }

    [Fact]
    public async Task O_on_a_row_with_one_link_opens_it_and_shows_no_list()
    {
        var panel = await Serving(Of(4));

        panel.OpenLinks(panel.Rows[0].Id);

        Assert.Equal(["sindri://item/ITM-0000000000000000000000000000000f"], _opened);
        Assert.False(panel.LinkList.IsOpen);
    }

    [Fact]
    public async Task O_on_a_row_with_no_link_does_nothing()
    {
        var panel = await Serving(Open("td_a", "no links"));

        panel.OpenLinks("td_a");

        Assert.Empty(_opened);
        Assert.False(panel.LinkList.IsOpen);
    }

    [Fact]
    public async Task O_on_a_row_with_three_links_opens_a_list_of_all_three_on_the_first()
    {
        var panel = await Serving(Three());

        panel.OpenLinks(panel.Rows[0].Id);

        Assert.True(panel.LinkList.IsOpen);
        Assert.Equal(["acme/api#12", "ENG-12", "ITM-0000000000000000000000000000000f"], panel.LinkList.Items.Select(i => i.Ref), StringComparer.Ordinal);
        Assert.Equal(0, panel.LinkList.SelectedIndex);
        Assert.Empty(_opened);
    }

    [Fact]
    public async Task J_and_K_move_in_the_list_and_stop_at_both_ends_and_Enter_opens_the_second()
    {
        var panel = await Serving(Three());
        panel.OpenLinks(panel.Rows[0].Id);

        panel.LinkList.Move(1);
        Assert.Equal(1, panel.LinkList.SelectedIndex);
        panel.LinkList.Move(1);
        panel.LinkList.Move(1);
        Assert.Equal(2, panel.LinkList.SelectedIndex);
        panel.LinkList.Move(-1);
        panel.LinkList.Move(-1);
        panel.LinkList.Move(-1);
        Assert.Equal(0, panel.LinkList.SelectedIndex);
        panel.LinkList.Move(1);
        Assert.True(panel.LinkList.Items[1].IsSelected);

        panel.OpenSelectedLink();

        Assert.Equal(["sindri://linear/ENG-12"], _opened);
        Assert.False(panel.LinkList.IsOpen);
    }

    [Fact]
    public async Task Esc_closes_the_list_and_opens_nothing()
    {
        var panel = await Serving(Three());
        panel.OpenLinks(panel.Rows[0].Id);

        Assert.True(panel.LinkList.Close());

        Assert.False(panel.LinkList.IsOpen);
        Assert.Empty(_opened);
        Assert.False(panel.LinkList.Close());
    }

    [Fact]
    public async Task The_list_closes_when_focus_leaves_the_panel()
    {
        var panel = await Serving(Three());
        panel.HasFocus = true;
        panel.OpenLinks(panel.Rows[0].Id);

        panel.HasFocus = false;

        Assert.False(panel.LinkList.IsOpen);
    }
}
