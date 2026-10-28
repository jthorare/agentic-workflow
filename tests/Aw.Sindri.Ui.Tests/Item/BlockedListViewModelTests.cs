using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Sandbox;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Item;

namespace Aw.Sindri.Ui.Tests.Item;

/// <summary>The item sheet's Blocked list and its Allow button (sandbox spec §8, states.json 'blockedList' and 'allow'), over the spec-shaped denials.</summary>
[Lane(TestLane.Unit)]
public sealed class BlockedListViewModelTests
{
    private const string Item = "ITM-0000000000000000000000000000000f";
    private const string Launch = "LCH-00000000000000000000000000000001";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<BlockedListViewModel> Read(string scenario)
    {
        _server.SandboxDenialsAnswer = RouteAnswer.SpecShaped("sandbox-denials", scenario);
        var list = new BlockedListViewModel(_server);
        await list.Read(_api, Item, Token);
        return list;
    }

    [Fact]
    public async Task Nothing_blocked_says_so_and_lists_no_row()
    {
        var list = await Read("none");

        Assert.Equal((Item, "Nothing was blocked", 0, false), (Assert.Single(_server.SandboxDenialItems), list.Summary, list.Rows.Count, list.HasSeeAll));
    }

    [Fact]
    public async Task A_short_list_shows_every_target_with_its_count_and_its_fix_by_kind()
    {
        var list = await Read("some");

        Assert.Equal(string.Empty, list.Summary);
        Assert.Equal(
            ["Blocked: registry.example.org × 3", "Blocked: /home/thor/.config/tool × 1", "Blocked: /home/thor/.ssh × 2", "Blocked: git push × 4"],
            list.Rows.Select(r => r.Line), StringComparer.Ordinal);
        Assert.Equal(
            [
                "Blocked host registry.example.org 3 times; allow it for this repo",
                "Blocked path /home/thor/.config/tool 1 times; allow it for this repo",
                "Blocked path /home/thor/.ssh: never allowed, Keys and tokens are never shared with a session.",
                "Blocked tool git push 4 times; push from your own terminal",
            ],
            list.Rows.Select(r => r.Text), StringComparer.Ordinal);
        Assert.Equal([true, true, false, false], list.Rows.Select(r => r.HasAllow));
    }

    [Fact]
    public async Task A_long_list_shows_the_top_five_and_See_all_blocked_shows_the_rest()
    {
        var list = await Read("many");

        Assert.Equal(("Blocked: top 5 of 8", 5, true, "See all blocked"), (list.Summary, list.Rows.Count, list.HasSeeAll, list.SeeAllText));
        Assert.Equal(["host0.example.org", "host1.example.org", "host2.example.org", "host3.example.org", "host4.example.org"], list.Rows.Select(r => r.Line["Blocked: ".Length..].Split(' ')[0]), StringComparer.Ordinal);

        list.ShowAll();

        Assert.Equal((8, false), (list.Rows.Count, list.HasSeeAll));
    }

    [Fact]
    public async Task Allow_sends_the_launch_kind_and_target_once_and_says_the_approval_is_needed()
    {
        var list = await Read("some");
        ApprovalRequestedEventArgs? requested = null;
        list.ApprovalRequested += (_, e) => requested = e;
        var row = list.Rows[0];

        await row.Allow(Token);

        Assert.Equal([new AllowRequest(Launch, "network", "registry.example.org")], _server.Allowed);
        Assert.Equal((AllowState.NeedsApproval, "Allow", "Needs your approval: open the approval to allow registry.example.org"), (row.State, row.AllowText, list.Voice.Announcement));
        Assert.True(list.Voice.IsAnnouncing);
        Assert.Equal("registry.example.org", requested?.Target);
    }

    [Fact]
    public async Task An_entry_the_approved_profile_already_has_says_Allowed_without_asking_for_an_approval()
    {
        var list = await Read("some");
        _server.SandboxAllowAnswer = RouteAnswer.SpecShaped("sandbox-allow", "allowed");
        var opened = 0;
        list.ApprovalRequested += (_, _) => opened++;

        await list.Rows[0].Allow(Token);

        Assert.Equal((AllowState.Allowed, "Allowed", "Allowed", 0), (list.Rows[0].State, list.Rows[0].AllowText, list.Voice.Announcement, opened));
    }

    [Fact]
    public async Task A_path_is_sent_as_a_path()
    {
        var list = await Read("some");

        await list.Rows[1].Allow(Token);

        Assert.Equal([new AllowRequest(Launch, "path", "/home/thor/.config/tool")], _server.Allowed);
    }

    [Fact]
    public async Task While_sending_the_button_stays_and_says_Sending_and_a_second_press_does_nothing()
    {
        var list = await Read("some");
        _server.SandboxAllowGate = new TaskCompletionSource();
        var row = list.Rows[0];

        var first = row.Allow(Token);
        await row.Allow(Token);

        Assert.Equal((AllowState.Sending, "Allow", "Sending", 1), (row.State, row.AllowText, list.Voice.Announcement, _server.Allowed.Count));

        _server.SandboxAllowGate.SetResult();
        await first;

        Assert.Equal(AllowState.NeedsApproval, row.State);
    }

    [Fact]
    public async Task Pressing_Allow_again_while_the_approval_is_open_asks_to_open_it_again_without_a_second_request()
    {
        var list = await Read("some");
        var opened = 0;
        list.ApprovalRequested += (_, _) => opened++;

        await list.Rows[0].Allow(Token);
        await list.Rows[0].Allow(Token);

        Assert.Equal((2, 1), (opened, _server.Allowed.Count));
    }

    [Fact]
    public async Task A_refusal_says_Refused_with_the_served_reason_and_a_session_refusal_the_same()
    {
        var list = await Read("some");
        _server.SandboxAllowAnswer = RouteAnswer.Problem("sandbox-allow", "refused");
        var row = list.Rows[1];

        await row.Allow(Token);

        Assert.Equal((AllowState.Refused, "Refused", "Refused"), (row.State, row.AllowText, list.Voice.Announcement));
        Assert.Contains("never shared with a session", row.Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Once_the_profile_is_approved_the_row_says_Allowed()
    {
        var list = await Read("some");
        _server.ProfileAnswer = RouteAnswer.Api("profile", "unapproved");
        await list.Rows[0].Allow(Token);

        await list.Read(_api, Item, Token);
        Assert.Equal(AllowState.NeedsApproval, list.Rows[0].State);

        _server.ProfileAnswer = RouteAnswer.Api("profile", "approved");
        await list.Read(_api, Item, Token);

        Assert.Equal((AllowState.Allowed, "Allowed", "Allowed"), (list.Rows[0].State, list.Rows[0].AllowText, list.Voice.Announcement));
    }

    [Fact]
    public async Task A_read_again_keeps_the_rows_so_the_focused_button_survives()
    {
        var list = await Read("some");
        var rows = list.Rows.ToArray();

        await list.Read(_api, Item, Token);

        Assert.All(rows.Zip(list.Rows), pair => Assert.Same(pair.First, pair.Second));
    }

    [Fact]
    public async Task A_failed_read_keeps_what_was_shown()
    {
        var list = await Read("some");
        _server.SandboxDenialsAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-001", "down");

        await list.Read(_api, Item, Token);

        Assert.Equal(4, list.Rows.Count);
    }

    [Fact]
    public async Task Moving_to_another_item_drops_the_list()
    {
        var list = await Read("some");

        list.Reset();

        Assert.Equal((0, string.Empty), (list.Rows.Count, list.Summary));
    }
}
