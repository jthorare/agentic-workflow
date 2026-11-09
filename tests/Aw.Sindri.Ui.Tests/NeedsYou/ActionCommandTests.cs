using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>ActionCommand.For fills a catalog command's placeholders from what the row knows; the catalog golden decides every literal.</summary>
[Lane(TestLane.Unit)]
public sealed class ActionCommandTests
{
    private const string Item = "ITEM-1";
    private const string RowCli = "sindri row served";

    private static string CatalogCli(string action) =>
        AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value.Rows.Single(r => r.Action == action).Cli;

    private static (string Cli, string Hint) For(string action, NeedsYouSubject? subject, string? item) =>
        ActionCommand.For(CatalogCli(action), isPrimary: false, RowCli, subject, item);

    [Fact]
    public void CloseItem_fills_the_item_and_leaves_its_unfilled_optional_group_as_served()
    {
        var (cli, hint) = For("CloseItem:done", subject: null, Item);

        Assert.Equal(CatalogCli("CloseItem:done").Replace("<itemId>", Item, StringComparison.Ordinal), cli);
        Assert.Empty(hint);
    }

    [Fact]
    public void Decide_fills_the_decision_id_and_leaves_the_choice_to_fill_in()
    {
        var (cli, hint) = For("Decide", new NeedsYouSubject(NeedsYouKinds.Decision, "DEC-7"), item: null);

        Assert.Equal(CatalogCli("Decide").Replace("<decisionId>", "DEC-7", StringComparison.Ordinal), cli);
        Assert.Equal(Labels.FillInHint(["choice"]), hint);
    }

    [Fact]
    public void StartInstance_fills_the_item_and_leaves_the_recipe_to_fill_in()
    {
        var (cli, hint) = For("StartInstance", subject: null, Item);

        Assert.Equal(CatalogCli("StartInstance").Replace("<itemId>", Item, StringComparison.Ordinal), cli);
        Assert.Equal(Labels.FillInHint(["recipe"]), hint);
    }

    [Fact]
    public void An_id_placeholder_is_not_the_item_and_stays_as_served()
    {
        var (cli, hint) = For("ReviewContextMap:drop", subject: null, Item);

        Assert.Equal(CatalogCli("ReviewContextMap:drop"), cli);
        Assert.Equal(Labels.FillInHint(["id"]), hint);
    }

    [Fact]
    public void The_hint_names_every_open_placeholder_once_in_the_order_they_appear()
    {
        var (_, hint) = For("Decide", subject: null, item: null);

        Assert.Equal(Labels.FillInHint(["decisionId", "choice"]), hint);
    }
}
