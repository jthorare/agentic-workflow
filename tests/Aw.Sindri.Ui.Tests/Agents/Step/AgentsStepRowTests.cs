using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Agents.Page;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Step;

/// <summary>An Agents row an engine launch started says which instance and Step it works on, and opens that frame (L2). The launch block is sessions-id/cursor-engine-launch.json's.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentsStepRowTests
{
    private const string Instance = "ITM-00000000000000000000000000000001/step/1";

    private readonly AgentsPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonNode Served(Action<JsonObject> cursorRow)
    {
        var list = AgentsPageFixture.Edited(_ => { });
        cursorRow(AgentsPageFixture.Row(list, "cursor/"));
        return list;
    }

    private static JsonObject Launch(string? instance, string? state, int? attempt) =>
        new() { ["instance"] = instance, ["state"] = state, ["attempt"] = attempt };

    private async Task<SessionRowViewModel> CursorRow(Action<JsonObject> edit)
    {
        _f.Serve(Served(edit));
        var page = await _f.Shown(Token);
        return page.Groups.SelectMany(g => g.Rows).Single(r => r.Ref.StartsWith("cursor/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_engine_launch_row_says_on_the_instance_and_the_state_with_the_attempt_above_one()
    {
        var row = await CursorRow(r => r["launch"] = Launch(Instance, "build", 2));

        Assert.Equal((true, "On a workflow step · build · attempt 2"), (row.HasStep, row.StepText));
        Assert.Contains("On a workflow step · build · attempt 2", row.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_first_attempt_is_not_named()
    {
        var row = await CursorRow(r => r["launch"] = Launch(Instance, "build", 1));

        Assert.Equal("On a workflow step · build", row.StepText);
    }

    [Fact]
    public async Task No_launch_row_and_an_attended_launch_draw_nothing_new()
    {
        var none = await CursorRow(r => r["launch"] = null);
        var attended = await CursorRow(r => r["launch"] = Launch(instance: null, state: null, attempt: null));

        Assert.Equal((false, string.Empty, false, string.Empty), (none.HasStep, none.StepText, attended.HasStep, attended.StepText));
        Assert.DoesNotContain(" on ", attended.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_on_a_step_row_opens_that_instances_frame_at_its_state()
    {
        _f.Serve(Served(r => r["launch"] = Launch(Instance, "build", 2)));
        var page = await _f.Shown(Token);
        SessionFrameRequestedEventArgs? asked = null;
        page.FrameRequested += (_, e) => asked = e;
        _ = page.Press("J");   // the first row is the launched one
        FocusAsked(page, out var focus);

        Assert.True(page.Press("O"));

        Assert.Equal((Instance, "build"), (asked!.Address, asked.State));
        Assert.Null(focus());
    }

    [Fact]
    public async Task O_on_a_row_without_a_step_still_moves_to_its_focus_line()
    {
        _f.Serve(Served(r => r["launch"] = Launch(instance: null, state: null, attempt: null)));
        var page = await _f.Shown(Token);
        var opened = false;
        page.FrameRequested += (_, _) => opened = true;
        _ = page.Press("J");
        FocusAsked(page, out var focus);

        Assert.True(page.Press("O"));

        Assert.False(opened);
        Assert.True(focus()!.ToFocusLine);
    }

    [Fact]
    public async Task Clicking_the_chip_asks_for_the_same_frame()
    {
        _f.Serve(Served(r => r["launch"] = Launch(Instance, "build", 2)));
        var page = await _f.Shown(Token);
        SessionFrameRequestedEventArgs? asked = null;
        page.FrameRequested += (_, e) => asked = e;

        page.Groups.SelectMany(g => g.Rows).Single(r => r.Ref.StartsWith("cursor/", StringComparison.Ordinal)).OpenFrame();

        Assert.Equal((Instance, "build"), (asked!.Address, asked.State));
    }

    [Fact]
    public async Task A_Step_with_no_state_still_opens_its_frame_from_the_chip_and_from_O()
    {
        _f.Serve(Served(r => r["launch"] = Launch(Instance, state: null, attempt: null)));
        var page = await _f.Shown(Token);
        var asked = new List<SessionFrameRequestedEventArgs>();
        page.FrameRequested += (_, e) => asked.Add(e);
        _ = page.Press("J");
        var row = page.Groups.SelectMany(g => g.Rows).Single(r => r.Ref.StartsWith("cursor/", StringComparison.Ordinal));

        row.OpenFrame();
        Assert.True(page.Press("O"));

        Assert.Equal((true, 2, "On a workflow step"), (row.HasStep, asked.Count, row.StepText));
        Assert.All(asked, e => Assert.Equal((Instance, null), (e.Address, e.State)));
        Assert.Equal("On a workflow step: open the run", row.StepName);
    }

    [Fact]
    public async Task The_chips_name_starts_with_its_visible_words_and_says_what_it_opens()
    {
        var row = await CursorRow(r => r["launch"] = Launch(Instance, "build", 2));

        Assert.Equal("On a workflow step · build · attempt 2: open the run at build", row.StepName);
    }

    private static void FocusAsked(AgentsPageViewModel page, out Func<AgentFocusEventArgs?> read)
    {
        AgentFocusEventArgs? last = null;
        page.FocusRequested += (_, e) => last = e;
        read = () => last;
    }
}
