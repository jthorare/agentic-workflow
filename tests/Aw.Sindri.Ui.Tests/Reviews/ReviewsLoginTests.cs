using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Reviews;
using static Aw.Sindri.Ui.Tests.Reviews.ReviewsFixture;

namespace Aw.Sindri.Ui.Tests.Reviews;

/// <summary>The Reviews tab's login field (review page spec §6.1, §6.3): Save sends review.login, the list reads again, and a refusal shows under the field.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewsLoginTests
{
    private readonly ReviewsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ReviewsPageViewModel> Prompt()
    {
        _f.Serve("no-login");
        return await _f.Shown(Token);
    }

    [Fact]
    public async Task Save_can_be_pressed_once_the_field_has_text()
    {
        var page = await Prompt();
        Assert.True(page.CanSaveLogin);   // the suggestion fills the field

        page.LoginField = string.Empty;
        Assert.False(page.CanSaveLogin);

        page.LoginField = "  ";
        Assert.False(page.CanSaveLogin);

        page.LoginField = "joi-example";
        Assert.True(page.CanSaveLogin);
    }

    [Fact]
    public async Task A_valid_login_is_sent_and_the_list_is_read_again()
    {
        var page = await Prompt();
        var reads = _f.Server.ReviewsCalls;
        page.LoginField = "joi-example";
        _f.Serve("some");

        await page.SaveLogin(Token);

        Assert.Equal(["joi-example"], _f.Server.ReviewLoginSaves.Select(s => s.Login), StringComparer.Ordinal);
        Assert.Equal(reads + 1, _f.Server.ReviewsCalls);
        Assert.False(page.IsLoginPrompt);
        Assert.Equal(2, page.Groups.Count);
        Assert.Equal(string.Empty, page.LoginError);
    }

    [Fact]
    public async Task A_bad_login_shows_the_servers_detail_under_the_field_and_reads_nothing()
    {
        var page = await Prompt();
        var reads = _f.Server.ReviewsCalls;
        page.LoginField = "-bad-";
        _f.Server.ReviewLoginAnswer = RouteAnswer.Problem("reviews-login", "bad-login");

        await page.SaveLogin(Token);

        Assert.Contains("review.login: '-bad-' isn't a GitHub login", page.LoginError, StringComparison.Ordinal);
        Assert.True(page.IsLoginPrompt);
        Assert.Equal(reads, _f.Server.ReviewsCalls);
        Assert.Equal("-bad-", page.LoginField);
    }

    [Fact]
    public async Task A_session_refusal_shows_its_text()
    {
        var page = await Prompt();
        _f.Server.ReviewLoginAnswer = RouteAnswer.Problem("reviews-login", "session");

        await page.SaveLogin(Token);

        Assert.Contains("review.login is the owner's to set", page.LoginError, StringComparison.Ordinal);
        Assert.Contains("SND-AUTH-002", page.LoginError, StringComparison.Ordinal);
        Assert.True(page.IsLoginPrompt);
    }

    [Fact]
    public async Task A_later_good_save_clears_the_failure_line()
    {
        var page = await Prompt();
        _f.Server.ReviewLoginAnswer = RouteAnswer.Problem("reviews-login", "bad-login");
        await page.SaveLogin(Token);
        _f.Server.ReviewLoginAnswer = RouteAnswer.Api("reviews-login", "saved");
        _f.Serve("some");

        await page.SaveLogin(Token);

        Assert.Equal(2, _f.Server.ReviewLoginSaves.Count);
        Assert.Equal(string.Empty, page.LoginError);
        Assert.False(page.IsLoginPrompt);
    }

    [Fact]
    public async Task A_refresh_does_not_overwrite_what_the_owner_typed()
    {
        var page = await Prompt();
        Assert.Equal("joi-example", page.LoginField);   // empty field: the suggestion fills it
        page.LoginField = "typed-by-owner";

        await page.Refresh(Url, Token);

        Assert.Equal("typed-by-owner", page.LoginField);
    }

    [Fact]
    public async Task A_refresh_keeps_the_same_row_objects_so_focus_stays_on_its_PR()
    {
        var page = await _f.Shown(Token);
        var before = page.Groups.SelectMany(g => g.Sections).SelectMany(s => s.Rows).ToDictionary(r => r.Ref, StringComparer.Ordinal);

        await page.Refresh(Url, Token);

        var after = page.Groups.SelectMany(g => g.Sections).SelectMany(s => s.Rows).ToList();
        Assert.Equal(before.Count, after.Count);
        Assert.All(after, r => Assert.Same(before[r.Ref], r));
    }

    [Fact]
    public async Task The_column_headings_come_from_the_labels()
    {
        var page = await _f.Shown(Token);

        Assert.Equal(
            [Labels.ReviewsColPr, Labels.ReviewsColAuthor, Labels.ReviewsColAge, Labels.ReviewsColCi, Labels.ReviewsColRequired, Labels.ReviewsColDoc],
            page.Columns,
            StringComparer.Ordinal);
    }
}
