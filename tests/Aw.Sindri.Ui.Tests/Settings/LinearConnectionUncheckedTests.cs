using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.Tests.Settings;

/// <summary>Settings → Connections → Linear: a key whose pointer is set but that no save has checked ('unchecked', no checkedAt) shows its set line, not the empty paste box.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearConnectionUncheckedTests
{
    private const string Words = "Not checked yet. Save to check.";

    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RouteAnswer Connection(string read, string write) =>
        new(HttpStatusCode.OK,
            $$"""{"read":{"state":"{{read}}","checkedAt":null},"write":{"state":"{{write}}","checkedAt":null},"teams":[{"id":"00000000-0000-4000-8000-0000000000e1","key":"ENG","name":"ENG"}],"pending":false}""");

    private async Task<LinearConnectionViewModel> Shown(string read, string write)
    {
        _server.LinearConnectionAnswer = Connection(read, write);
        var card = new LinearConnectionViewModel(() => _api, _server);
        await card.Refresh(_api, Token);
        return card;
    }

    [Fact]
    public async Task An_unchecked_read_key_shows_its_set_line_not_the_paste_box()
    {
        var card = await Shown("unchecked", "notSet");

        Assert.Equal((true, false, "Read access: " + Words), (card.ShowReadSet, card.ShowReadPaste, card.ReadAccessText));
    }

    [Fact]
    public async Task An_unchecked_read_key_is_not_claimed_as_a_read_connection_in_the_summary()
    {
        var card = await Shown("unchecked", "notSet");

        Assert.Equal((false, Labels.LinearConnNotConnected), (card.ReadIsSet, card.Summary));
    }

    [Fact]
    public async Task Replace_key_on_an_unchecked_read_key_opens_the_paste_box_and_lists_with_the_stored_team_ticked()
    {
        var card = await Shown("unchecked", "notSet");
        card.ReplaceRead();
        card.ReadKey = "lin_api_new";

        await card.ListTeams(Token);

        Assert.Equal((false, true, true), (card.ShowReadSet, card.ShowReadPaste, card.ShowCancelRead));
        Assert.Equal([true, false], [.. card.Teams.Select(t => t.IsTicked)]);
    }

    [Fact]
    public async Task An_unchecked_write_key_shows_its_set_line_with_Replace_and_Remove_reachable()
    {
        var card = await Shown("resolves", "unchecked");

        Assert.Equal((true, true, false, "Write access: " + Words), (card.ShowWrite, card.ShowWriteSet, card.ShowWritePaste, card.WriteAccessText));
    }

    [Fact]
    public async Task Replace_key_on_an_unchecked_write_key_opens_the_paste_box_with_Cancel()
    {
        var card = await Shown("resolves", "unchecked");

        card.ReplaceWrite();

        Assert.Equal((false, true, true), (card.ShowWriteSet, card.ShowWritePaste, card.ShowCancelWrite));
    }

    [Fact]
    public async Task An_unchecked_write_key_under_an_unchecked_read_key_still_draws_its_line()
    {
        var card = await Shown("unchecked", "unchecked");

        Assert.Equal((true, true, false), (card.ShowWrite, card.ShowWriteSet, card.ShowWritePaste));
    }
}
