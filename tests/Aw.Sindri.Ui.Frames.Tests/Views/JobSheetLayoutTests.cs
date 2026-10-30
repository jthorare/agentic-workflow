using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using static Aw.Sindri.Ui.Tests.Jobs.JobsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The job sheet in its window (F3 jobs spec §2, §5): the red callout with the served sentence and error code on a failed job, What it produced on a finished one, and Cancel job asking first with Keep putting the sheet back.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class JobSheetLayoutTests : FrameHost
{
    private async Task<(Window Window, ShellViewModel Shell)> Sheet(string scenario)
    {
        Server.JobsAnswer = RouteAnswer.Api("jobs", "running");
        Server.JobAnswers[Id] = RouteAnswer.Api("jobs-id", scenario);
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.Navigate(new DeepLink(DeepLinkKind.Job, Id));
        await Until(window, () => shell.JobSheet.Phase == FramePhase.Shown && shell.IsJobSheet, "the job sheet never drew the job");
        return (window, shell);
    }

    [Fact]
    public Task A_failed_job_shows_its_sentence_and_error_code_in_a_red_callout_and_offers_no_Cancel() => OnUi(async () =>
    {
        var (window, _) = await Sheet("failed");

        var callout = Named<Border>(window, "JobSummary");
        Assert.True(callout.IsEffectivelyVisible);
        Assert.Contains(callout.Classes, c => string.Equals(c, "failed", StringComparison.Ordinal));
        Assert.Equal("it broke", ((TextBlock)((StackPanel)callout.Child!).Children[0]).Text);
        Assert.Equal("SND-SCHED-001", Named<TextBlock>(window, "JobSummaryCode").Text);
        Assert.True(Visible(window, "JobSummaryCode"));
        Assert.False(Visible(window, "JobCancel"));
        Frame(window, "sheet-failed", "jobs-");
    });

    [Fact]
    public Task A_job_that_ended_well_has_no_callout_code_and_its_result_waits_under_What_it_produced() => OnUi(async () =>
    {
        var (window, shell) = await Sheet("result");

        Assert.False(Visible(window, "JobSummaryCode"));
        var result = Named<Expander>(window, "JobResult");
        Assert.True(result.IsEffectivelyVisible);
        Assert.False(result.IsExpanded);   // closed until asked for

        OpenDisclosures(window);

        Assert.NotEmpty(shell.JobSheet.ResultText);
        Assert.Equal(shell.JobSheet.ResultText, ((TextBlock)Named<Border>(window, "JobResultBody").Child!).Text);
        Assert.True(Visible(window, "JobResultBody"));
    });

    [Fact]
    public Task Cancel_job_asks_first_and_Keep_puts_the_sheet_back_without_sending() => OnUi(async () =>
    {
        var (window, _) = await Sheet("running");
        var cancel = Named<Control>(window, "JobCancel");

        Named<Button>(cancel, "RunButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.True(Visible(cancel, "ConfirmRow"));
        Assert.True(Visible(cancel, "ConfirmButton"));
        Assert.True(Visible(cancel, "KeepButton"));
        Assert.Empty(Server.CancelledJobs);

        Named<Button>(cancel, "KeepButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.False(Visible(cancel, "ConfirmRow"));
        Assert.True(Visible(cancel, "RunButton"));
        Assert.Empty(Server.CancelledJobs);
    });
}
