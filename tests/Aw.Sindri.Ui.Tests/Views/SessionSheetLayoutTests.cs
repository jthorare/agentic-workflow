using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Agents;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The session sheet's layout in a real window (agents spec §2, §7): the header with how long in its status and the ref's copy chip, the amber callout of a session that is asking, and the fields in captioned groups with a 150 px label column.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class SessionSheetLayoutTests : FrameHost
{
    private const string Claude = "claude-code/aaaaaaaa-0000-4000-8000-000000000001";
    private const string Codex = "codex/bbbbbbbb-0000-4000-8000-000000000002";

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Opened(string reference)
    {
        var (window, shell) = await Connected(width: 1600, height: 1000);
        shell.GoTo(PageId.Agents);
        Settle(window);
        shell.Agents.RequestOpen(new SessionOpenRequestedEventArgs(new DeepLink(DeepLinkKind.Session, reference)));
        await Until(window, () => shell.IsSheetShown && shell.AgentSheet.Phase == FramePhase.Shown, "the sheet never showed the session");
        return (window, shell);
    }

    private static T InSheet<T>(Window window, string name)
        where T : Control => Named<Control>(window, "SessionSheet").GetVisualDescendants().OfType<T>().First(c => string.Equals(c.Name, name, StringComparison.Ordinal));

    private static bool InSheetVisible(Window window, string name) =>
        Named<Control>(window, "SessionSheet").GetVisualDescendants().OfType<Control>().Any(c => string.Equals(c.Name, name, StringComparison.Ordinal) && c.IsEffectivelyVisible);

    private static void OpenDetails(Window window)
    {
        InSheet<Expander>(window, "SessionDetails").IsExpanded = true;
        Settle(window);
    }

    [Fact]
    public Task A_session_that_is_asking_shows_the_amber_callout_and_one_that_is_not_does_not() => OnUi(async () =>
    {
        var (asking, _) = await Opened(Codex);
        Assert.True(InSheetVisible(asking, "SessionAskingCallout"));
        Assert.Equal("Answer it in its own terminal; Sindri only shows that it is waiting.", InSheet<Border>(asking, "SessionAskingCallout").GetVisualDescendants().OfType<TextBlock>().Single().Text);
    });

    [Fact]
    public Task A_working_session_has_no_callout() => OnUi(async () =>
    {
        var (window, _) = await Opened(Claude);

        Assert.False(InSheetVisible(window, "SessionAskingCallout"));
    });

    [Fact]
    public Task The_header_says_how_long_in_the_status_and_offers_the_refs_copy_chip() => OnUi(async () =>
    {
        var (window, shell) = await Opened(Claude);

        Assert.StartsWith("In this status for ", InSheet<Border>(window, "SessionInStatus").GetVisualDescendants().OfType<TextBlock>().Single().Text, StringComparison.Ordinal);
        Assert.True(InSheetVisible(window, "SessionRefCopy"));
        Assert.Equal("Copy reference", shell.AgentSheet.RefCopy!.Label);
    });

    [Fact]
    public Task The_fields_sit_in_captioned_groups_with_a_150_px_label_column() => OnUi(async () =>
    {
        var (window, shell) = await Opened(Claude);
        OpenDetails(window);

        Assert.Equal(["Where it is working", "Timing"], shell.AgentSheet.Sections.Select(s => s.Heading), StringComparer.Ordinal);
        var heads = InSheet<ItemsControl>(window, "SessionFields").GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("eyebrow")).Select(t => t.Text).ToList();
        Assert.Equal(["Where it is working", "Timing"], heads, StringComparer.Ordinal);
        Assert.True(InSheetVisible(window, "SessionHostHeading"));
        var rows = Named<Control>(window, "SessionSheet").GetVisualDescendants().OfType<Border>().Where(b => (string.Equals(b.Name, "SessionField", StringComparison.Ordinal) || string.Equals(b.Name, "SessionHostField", StringComparison.Ordinal)) && b.IsEffectivelyVisible).ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal(150, r.GetVisualDescendants().OfType<Grid>().First().ColumnDefinitions[0].Width.Value));
    });
}
