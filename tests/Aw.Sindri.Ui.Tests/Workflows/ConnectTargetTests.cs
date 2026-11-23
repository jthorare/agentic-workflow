using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The edit bar's connect list names steps the way their cards do (#3684): never by id.</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectTargetTests
{
    private static GraphNodeViewModel Node(string id, string title, string line) => new(id, title, "code", 0, 0, status: null) { Line = line };

    [Fact]
    public void Each_step_shows_its_card_title_and_no_id()
    {
        var list = ConnectTarget.ListOf([Node("fetch", "Fetch upstream status", "JavaScript · 8 lines"), Node("step_2", "Code", "JavaScript · 3 lines")]);

        Assert.Equal([("fetch", "Fetch upstream status"), ("step_2", "Code")], list.Select(t => (t.Id, t.Text)));
    }

    [Fact]
    public void Two_steps_with_one_title_add_their_settings_line_and_then_their_place()
    {
        var list = ConnectTarget.ListOf([Node("a", "Code", "JavaScript · 8 lines"), Node("b", "Code", "JavaScript · 3 lines"), Node("c", "Code", "JavaScript · 3 lines"), Node("d", "Notify", "Notify: done")]);

        Assert.Equal(
            ["Code · JavaScript · 8 lines", "Code · JavaScript · 3 lines (1)", "Code · JavaScript · 3 lines (2)", "Notify"],
            list.Select(t => t.Text),
            StringComparer.Ordinal);
    }
}
