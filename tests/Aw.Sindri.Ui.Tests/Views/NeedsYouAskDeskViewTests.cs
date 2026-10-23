using System.Net;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The ask desk in the Needs you action bar (engine #817): a field per question, options as buttons, Send answers off until every field has words.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class NeedsYouAskDeskViewTests : FrameHost
{
    [Fact]
    public Task The_bar_draws_every_question_with_a_field_and_options_and_Send_answers_waits_for_all() => OnUi(async () =>
    {
        Server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("workflow-instance", "lens-ask-multi"));
        Server.FactBody = (_, key) => string.Equals(key, "fact:escalation", StringComparison.Ordinal)
            ? new RouteAnswer(HttpStatusCode.OK, """{"key":"fact:escalation","type":"payload","body":[{"id":"q1","question":"Keep the legacy flow?","options":["Keep it","Drop it"],"lens":"product"},{"id":"q2","question":"Which table owns expiry?"}],"truncated":false}""")
            : null;
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "desk-task");
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, width: 1440, height: 900);
        shell.OpenNeedsYouInbox();
        await shell.Inbox.Bar.Loaded;
        await Until(window, () => shell.Inbox.Bar.HasQuestions, "the bar never drew the questions");

        Assert.True(Visible(window, "NeedsYouQuestions"));
        var prompts = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "NeedsYouQuestionPrompt" && t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Equal(["Keep the legacy flow?", "Which table owns expiry?"], prompts, StringComparer.Ordinal);
        Assert.Equal(2, window.GetVisualDescendants().OfType<TextBox>().Count(t => t.Name == "NeedsYouQuestionAnswer" && t.IsEffectivelyVisible));
        var approve = Named<Button>(window, "NeedsYouApprove");
        Assert.False(approve.IsEnabled);

        var option = window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && string.Equals(b.Content as string, "Drop it", StringComparison.Ordinal));
        option.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        shell.Inbox.Bar.Questions[1].Answer = "expiry_rules";
        Settle(window);

        Assert.Equal("Drop it", shell.Inbox.Bar.Questions[0].Answer);
        Assert.True(approve.IsEnabled);
    });
}
