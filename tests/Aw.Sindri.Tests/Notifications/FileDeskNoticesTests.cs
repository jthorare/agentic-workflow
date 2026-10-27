using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Notifications;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Notifications;

/// <summary>
/// Notifications (engine spec §6.3; README X5, X50; engine P6 unit (c)): each notice is one line of
/// '$AW_SINDRI_HOME/notifications.jsonl'; an interrupting one is also shown as an OS notification; an info one isn't, and
/// is collected into the day's digest. A repeat of an identity writes and shows nothing.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class FileDeskNoticesTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly FakeOsNotifier _os = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _home.Dispose();

    [Fact]
    public async Task Each_notice_is_one_line_of_notifications_jsonl()
    {
        var notices = Notices();

        _ = await notices.Send(DeskNoticesContract.Notice("WFI-a:2:0", NoticeClass.Info), Token);
        _ = await notices.Send(DeskNoticesContract.Notice("WFI-a:1:n1"), Token);

        var lines = await File.ReadAllLinesAsync(Path.Combine(_home.Path, "notifications.jsonl"), Token);
        Assert.Equal(2, lines.Length);
        var first = JsonDocument.Parse(lines[0]).RootElement;
        Assert.Equal(("WFI-a:2:0", "T-4/dev-notify/1", "T-4", "info"),
            (first.GetProperty("identity").GetString(), first.GetProperty("instance").GetString(), first.GetProperty("item").GetString(), first.GetProperty("class").GetString()));
        Assert.Equal("Waiting for the desk notification to be delivered.", first.GetProperty("text").GetString());
        Assert.Equal("interrupt", JsonDocument.Parse(lines[1]).RootElement.GetProperty("class").GetString());
    }

    [Fact]
    public async Task An_interrupt_is_shown_and_an_info_notice_goes_to_the_days_digest()
    {
        var notices = Notices();

        _ = await notices.Send(DeskNoticesContract.Notice("WFI-a:1:n1"), Token);
        _ = await notices.Send(DeskNoticesContract.Notice("WFI-a:2:n2", NoticeClass.Info), Token);

        var (title, text) = Assert.Single(_os.Shown);
        Assert.Equal(("Sindri", "Waiting for the desk notification to be delivered."), (title, text));
        var digest = Assert.Single(await File.ReadAllLinesAsync(Path.Combine(_home.Path, "digest", "2026-10-13.jsonl"), Token));
        Assert.Equal("WFI-a:2:n2", JsonDocument.Parse(digest).RootElement.GetProperty("identity").GetString());
    }

    [Fact]
    public async Task An_interrupt_carries_the_deep_link_to_the_instance_so_a_click_can_open_it()
    {
        var notices = Notices();

        _ = await notices.Send(DeskNoticesContract.Notice("WFI-a:1:n1"), Token);

        Assert.Equal(["workflow/T-4/dev-notify/1"], _os.Links);
    }

    [Fact]
    public async Task A_repeat_writes_and_shows_nothing()
    {
        var notices = Notices();

        _ = await notices.Send(DeskNoticesContract.Notice("WFI-a:1:n1"), Token);
        var again = await notices.Send(DeskNoticesContract.Notice("WFI-a:1:n1"), Token);

        Assert.False(again.Value);
        _ = Assert.Single(await File.ReadAllLinesAsync(Path.Combine(_home.Path, "notifications.jsonl"), Token));
        _ = Assert.Single(_os.Shown);
    }

    /// <summary>A restart builds a new instance on the same home: it seeds what was written, so a re-sent identity writes and shows nothing (PR 232 run 2, I7).</summary>
    [Fact]
    public async Task A_repeat_through_a_new_instance_on_the_same_home_writes_and_shows_nothing()
    {
        _ = await Notices().Send(DeskNoticesContract.Notice("WFI-a:1:n1"), Token);

        var again = await Notices().Send(DeskNoticesContract.Notice("WFI-a:1:n1"), Token);

        Assert.False(again.Value);
        _ = Assert.Single(await File.ReadAllLinesAsync(Path.Combine(_home.Path, "notifications.jsonl"), Token));
        _ = Assert.Single(_os.Shown);
    }

    [Fact]
    public async Task A_notice_with_a_row_writes_both_fields_and_hands_the_deep_link_to_the_notifier()
    {
        var row = NeedsYouRowIds.Park(InstanceAddress.From("T-4/dev-notify/1")).Value;
        var notice = DeskNoticesContract.Notice("WFI-a:1:n1") with { RowId = NeedsYouRowId.From(row), DeepLink = "needs-you/" + row };

        _ = await Notices().Send(notice, Token);

        var line = JsonDocument.Parse(Assert.Single(await File.ReadAllLinesAsync(Path.Combine(_home.Path, "notifications.jsonl"), Token))).RootElement;
        Assert.Equal((row, "needs-you/" + row), (line.GetProperty("rowId").GetString(), line.GetProperty("deepLink").GetString()));
        Assert.Equal("needs-you/" + row, Assert.Single(_os.Links));
    }

    [Fact]
    public async Task A_notice_without_a_row_writes_neither_field_and_a_line_from_before_them_still_reads()
    {
        var old = """{"identity":"WFI-a:1:n0","instance":"T-4/dev-notify/1","item":"T-4","class":"interrupt","text":"old","at":"2026-10-13T12:00:00+00:00"}""";
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "notifications.jsonl"), old + "\n", Token);
        var notices = Notices();

        var again = await notices.Send(DeskNoticesContract.Notice("WFI-a:1:n0"), Token);   // seeded from the old line: a repeat
        _ = await notices.Send(DeskNoticesContract.Notice("WFI-a:1:n1"), Token);

        Assert.False(again.Value);
        var added = JsonDocument.Parse((await File.ReadAllLinesAsync(Path.Combine(_home.Path, "notifications.jsonl"), Token))[1]).RootElement;
        Assert.False(added.TryGetProperty("rowId", out _) || added.TryGetProperty("deepLink", out _));
        Assert.Equal("workflow/T-4/dev-notify/1", Assert.Single(_os.Links));   // no row: the instance link
    }

    [Fact]
    public async Task A_banner_is_titled_with_the_item_title_not_the_instance_address()
    {
        var notices = Notices();

        _ = await notices.Send(DeskNoticesContract.Notice("WFI-a:1:n1") with { ItemTitle = "P-FRN-920 Singular Backend Model for Targets" }, Token);

        var (title, _) = Assert.Single(_os.Shown);
        Assert.Equal("P-FRN-920 Singular Backend Model for Targets", title);
    }

    private FileDeskNotices Notices() =>
        new(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), _os, SliceHarness.Json);
}
