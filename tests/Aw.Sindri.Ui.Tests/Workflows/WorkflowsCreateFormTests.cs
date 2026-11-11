using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The New workflow form's three #2688 fixes: the server's taken-id refusal in the form's own words, Create off until the short id changes, the empty text hidden while the form is open.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsCreateFormTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> Opened()
    {
        _f.ServeList(Golden("workflows", "list-empty"));
        var page = await _f.Shown(Token);
        page.OpenCreate();
        return page;
    }

    private void ServerHoldsTheSlug() =>
        _f.Server.CreateWorkflowAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-GRAPH-009", "the slug 'morning-check' belongs to another workflow; pick another");

    [Fact]
    public async Task The_servers_taken_id_refusal_reads_in_the_forms_own_words_never_the_raw_text()
    {
        var page = await Opened();
        ServerHoldsTheSlug();
        page.NewName = "Morning check";

        await page.Create(Token);

        Assert.Equal("A workflow already uses 'morning-check'; pick another short id.", page.CreateError);
        Assert.DoesNotContain("slug", page.CreateError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("SND-GRAPH-009", page.CreateErrorCode);
    }

    [Fact]
    public async Task After_the_taken_id_refusal_Create_stays_off_until_the_short_id_changes()
    {
        var page = await Opened();
        ServerHoldsTheSlug();
        page.NewName = "Morning check";
        await page.Create(Token);

        Assert.False(page.CanCreate);
        page.NewSlug = "morning-check-2";

        Assert.True(page.CanCreate);
    }

    [Fact]
    public async Task The_empty_text_and_the_agent_hint_are_hidden_while_the_form_is_open_and_back_when_it_closes()
    {
        _f.ServeList(Golden("workflows", "list-empty"));
        var page = await _f.Shown(Token);
        Assert.True(page.ShowsEmpty);

        page.OpenCreate();
        Assert.False(page.ShowsEmpty);

        page.CancelCreate();
        Assert.True(page.ShowsEmpty);
    }

    [Fact]
    public async Task A_hand_edited_short_id_survives_the_name_being_cleared_and_retyped()
    {
        var page = await Opened();
        page.NewName = "Daily";
        page.NewSlug = "my-id";

        page.NewName = string.Empty;
        page.NewName = "Weekly";

        Assert.Equal("my-id", page.NewSlug);
    }

    [Fact]
    public async Task Changing_the_name_after_a_refusal_with_a_hand_edited_short_id_keeps_the_taken_reason_on_screen()
    {
        var page = await Opened();
        ServerHoldsTheSlug();
        page.NewName = "Morning check";
        page.NewSlug = "my-id";
        await page.Create(Token);

        page.NewName = "Morning checks";

        Assert.Equal(("my-id", false, Labels.WfShortIdTaken("my-id"), true), (page.NewSlug, page.CanCreate, page.CreateReasonText, page.CreateReasonIsError));
    }

    [Fact]
    public async Task Typing_the_refused_short_id_back_after_changing_it_says_taken_again()
    {
        var page = await Opened();
        ServerHoldsTheSlug();
        page.NewName = "Morning check";
        await page.Create(Token);

        page.NewSlug = "other-id";
        Assert.True(page.CanCreate);
        page.NewSlug = "morning-check";

        Assert.Equal((false, Labels.WfShortIdTaken("morning-check"), true), (page.CanCreate, page.CreateReasonText, page.CreateReasonIsError));
    }
}
