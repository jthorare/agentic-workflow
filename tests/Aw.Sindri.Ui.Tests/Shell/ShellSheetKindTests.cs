using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Theme;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The detail sheet keeps the kind of link that opened it (screens spec §3), and the window has one sheet layer that chooses its content by that kind.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellSheetKindTests
{
    private static async Task<ShellViewModel> Shell()
    {
        var shell = new ShellViewModel(new InMemoryEndpointRecords(), new FakeSindriApi(), new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(TestContext.Current.CancellationToken);
        return shell;
    }

    private static DeepLink Link(string text) => DeepLink.TryParse(text, out var link) ? link : throw new InvalidOperationException(text);

    [Fact]
    public async Task The_same_subject_under_two_kinds_gives_two_sheet_states()
    {
        var shell = await Shell();

        shell.Navigate(Link("item/X"));
        var item = shell.SheetLink;
        shell.Navigate(Link("workflow/X"));
        var workflow = shell.SheetLink;

        Assert.Equal(Link("item/X"), item);
        Assert.Equal(Link("workflow/X"), workflow);
        Assert.Equal("X", shell.SheetSubject);
    }

    [Fact]
    public async Task The_edge_tab_reopens_the_kind_that_was_open()
    {
        var shell = await Shell();
        shell.Navigate(Link("item/A"));
        shell.Navigate(Link("recipe/workflow.project-build"));
        shell.CloseSheet();

        shell.ReopenSheet();

        Assert.True(shell.IsSheetOpen);
        Assert.Equal(Link("recipe/workflow.project-build"), shell.SheetLink);
        Assert.Equal("workflow.project-build", shell.SheetSubject);
    }

    [Fact]
    public async Task There_is_no_sheet_link_before_a_subject_is_opened()
    {
        var shell = await Shell();

        Assert.Null(shell.SheetLink);
        Assert.Equal(string.Empty, shell.SheetSubject);
        Assert.False(shell.HasEdgeTab);
    }

    [Fact]
    public async Task Before_any_sheet_is_opened_the_placeholder_shows()
    {
        var shell = await Shell();

        Assert.True(shell.IsSheetPlaceholderShown);
    }

    [Fact]
    public async Task The_placeholder_shows_for_every_kind_until_a_lane_claims_it()
    {
        var shell = await Shell();

        foreach (var kind in Enum.GetValues<DeepLinkKind>())
        {
            shell.OpenSheet(new DeepLink(kind, kind == DeepLinkKind.HomeNeedsYou ? null : "X"));

            Assert.Equal(kind is not (DeepLinkKind.Item or DeepLinkKind.NeedsYou or DeepLinkKind.Decision or DeepLinkKind.Workflow or DeepLinkKind.Recipe or DeepLinkKind.Session or DeepLinkKind.Job), shell.IsSheetPlaceholderShown);   // Job is the Jobs lane's, Session is U11a's, Recipe is U10's, Workflow is U8's, Item is U2's, NeedsYou and Decision are U3''s
            var noun = Labels.KindNoun(kind);
            var subject = kind == DeepLinkKind.HomeNeedsYou ? string.Empty : "X";
            Assert.Equal(noun.Length > 0 ? noun + " " + subject : subject, shell.SheetName);
        }
    }

    [Fact]
    public async Task An_item_link_claims_the_sheet_from_the_placeholder()
    {
        var shell = await Shell();

        shell.OpenSheet(Link("item/ITM-01JC8"));

        Assert.Equal((false, true, "ITM-01JC8"), (shell.IsSheetPlaceholderShown, shell.IsItemSheet, shell.SheetName));
    }

    [Fact]
    public async Task Opening_the_sheet_raises_the_placeholder_flag()
    {
        var shell = await Shell();
        var seen = new List<string>();
        shell.PropertyChanged += (_, e) => seen.Add(e.PropertyName ?? string.Empty);

        shell.OpenSheet(Link("item/X"));

        Assert.Contains(seen, name => string.Equals(name, nameof(ShellViewModel.IsSheetPlaceholderShown), StringComparison.Ordinal));
        Assert.Contains(seen, name => string.Equals(name, nameof(ShellViewModel.SheetName), StringComparison.Ordinal));
        Assert.Contains(seen, name => string.Equals(name, nameof(ShellViewModel.EdgeTabName), StringComparison.Ordinal));
    }

    [Fact]
    public void The_sheet_body_is_the_placeholder_and_shows_by_the_flag()
    {
        var xaml = File.ReadAllText(Path.Combine(UiSource.Library, "Views", "Shell", "ShellWindow.axaml"));
        var body = xaml[xaml.IndexOf("x:Name=\"SheetBody\"", StringComparison.Ordinal)..];
        body = body[..body.IndexOf("/>", StringComparison.Ordinal)];

        Assert.Contains("IsVisible=\"{Binding IsSheetPlaceholderShown}\"", body, StringComparison.Ordinal);
    }

    // a11y-1: the kind is part of the name, so "workflow/X" and "recipe/X" are not both "X". Ids that name themselves (ITM-, REQ-, JOB-) stay bare.
    [Theory]
    [InlineData("workflow/WFI-1", "Workflow WFI-1", "Reopen workflow WFI-1")]
    [InlineData("recipe/workflow.project", "Recipe workflow.project", "Reopen recipe workflow.project")]
    [InlineData("node/src.Api", "Node src.Api", "Reopen node src.Api")]
    [InlineData("session/codex/S1", "Session codex/S1", "Reopen session codex/S1")]
    [InlineData("system/docker", "System docker", "Reopen system docker")]
    [InlineData("service/serena", "Service serena", "Reopen service serena")]
    [InlineData("item/ITM-1", "ITM-1", "Reopen ITM-1")]
    [InlineData("decision/REQ-1", "REQ-1", "Reopen REQ-1")]
    [InlineData("job/JOB-1", "Job JOB-1", "Reopen job JOB-1")]
    public async Task The_sheet_and_its_edge_tab_are_named_with_the_kind(string link, string sheet, string edge)
    {
        var shell = await Shell();

        shell.Navigate(Link(link));

        Assert.Equal(sheet, shell.SheetName);
        Assert.Equal(edge, shell.EdgeTabName);
    }

    [Fact]
    public void The_sheet_region_and_its_title_are_named_by_SheetName_and_the_edge_tab_by_EdgeTabName()
    {
        var xaml = File.ReadAllText(Path.Combine(UiSource.Library, "Views", "Shell", "ShellWindow.axaml"));

        Assert.Contains("x:Name=\"Sheet\" Classes=\"sheet focus-region\" IsVisible=\"{Binding IsSheetShown}\" Focusable=\"True\"\n                AutomationProperties.Name=\"{Binding SheetName}\"", xaml.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Matches(@"x:Name=""SheetTitle""[^>]*AutomationProperties\.Name=""\{Binding SheetName\}""", xaml.ReplaceLineEndings(" "));
        Assert.Contains("x:Name=\"EdgeTab\"", xaml, StringComparison.Ordinal);
        Assert.Matches(@"x:Name=""EdgeTab""[^>]*AutomationProperties\.Name=""\{Binding EdgeTabName\}""", xaml.ReplaceLineEndings(" "));
    }
}
