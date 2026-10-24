using System.Net;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The scoping interview in the Needs you action bar (q65 d): every question with its choices, the recommended answer marked, Send answers off until every question has a pick.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class NeedsYouScopingViewTests : FrameHost
{
    [Fact]
    public Task The_bar_draws_each_question_with_its_choices_and_Send_answers_waits_for_every_pick() => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "desk-task");
        Server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("workflow-instance", "scoping-interview"));
        Server.FactBody = (_, key) => string.Equals(key, "fact:questions", StringComparison.Ordinal) ? new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("workflow-instance-fact", "scoping-questions")) : null;
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, width: 1440, height: 900);
        shell.OpenNeedsYouInbox();
        await shell.Inbox.Bar.Loaded;
        await Until(window, () => shell.Inbox.Bar.HasScoping, "the bar never drew the interview");

        Assert.True(Visible(window, "NeedsYouScoping"));
        var bar = shell.Inbox.Bar;
        var prompts = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "NeedsYouScopingPrompt" && t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Equal(bar.Scoping.Select(q => q.Prompt), prompts, StringComparer.Ordinal);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && string.Equals(t.Text, "Recommended", StringComparison.Ordinal));
        var approve = Named<Button>(window, "NeedsYouApprove");
        Assert.False(approve.IsEnabled);

        foreach (var question in bar.Scoping)
        {
            var choice = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "NeedsYouScopingChoice" && ReferenceEquals(b.DataContext, question.Choices[0]));
            choice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        Settle(window);
        Assert.True(approve.IsEnabled);
        Assert.True(Visible(window, "NeedsYouDoneAsking"));
    });
}
