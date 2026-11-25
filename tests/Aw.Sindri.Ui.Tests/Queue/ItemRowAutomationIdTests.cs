using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.Tests.Queue;

/// <summary>
/// The row's automation id (#2819): the Queue page draws an item in the open groups and in the closed fold from two reads, so for
/// one refresh an item closed between them is in both lists; the two rows must not share an id, or a find-by-id gets the first.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ItemRowAutomationIdTests
{
    private static ItemRowViewModel Row(string group) =>
        new(new ItemSummaryView("ITM-01JN2", "Try the queue", "agentic-workflow", "done", Step: null, A.Noon.AddMinutes(-12), PrimaryAction: null, Actions: []), group, primaryAction: null, A.Noon);

    [Fact]
    public void An_open_row_and_a_closed_row_of_one_item_get_different_ids() =>
        Assert.Equal(("ItemRow.ITM-01JN2", "ItemRow.closed.ITM-01JN2"), (Row("running").AutomationId, Row("closed").AutomationId));
}
