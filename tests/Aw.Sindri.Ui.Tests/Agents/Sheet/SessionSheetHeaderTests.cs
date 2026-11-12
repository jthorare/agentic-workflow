using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Agents;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Agents.Sheet;

/// <summary>The session sheet's header, details and action, and which sheet titles are set in mono (#2811): the working directory is up front, words are plain and only ids are mono.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionSheetHeaderTests
{
    private const string ClaudeRef = "claude-code/aaaaaaaa-0000-4000-8000-000000000001";
    private const string CodexRef = "codex/bbbbbbbb-0000-4000-8000-000000000002";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<SessionSheetViewModel> Opened(string reference)
    {
        var sheet = new SessionSheetViewModel(_server);
        var shown = new CatalogViewModel(_clipboard, _clock);
        shown.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        sheet.Wire(shown, _clock);
        await sheet.Open(_api, reference, Token);
        return sheet;
    }

    [Fact]
    public async Task Header_shows_repo_cwd_and_state_colour()
    {
        var sheet = await Opened(ClaudeRef);

        Assert.Equal("api · ideas", sheet.Subtitle);   // repo, then the worktree's folder
        Assert.DoesNotContain("private title", sheet.Subtitle, StringComparison.Ordinal);                                      // the title shows once
        Assert.Equal("/work/api/.claude/worktrees/ideas", sheet.WorkingDirectory);
        Assert.Equal("working", sheet.StateTone);                                                                              // the list's colour
        Assert.StartsWith("In this status", sheet.InStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Copy_working_directory()
    {
        var sheet = await Opened(ClaudeRef);

        await sheet.CopyWorkingDirectory(Token);

        Assert.Equal("/work/api/.claude/worktrees/ideas", _clipboard.Text);
    }

    [Fact]
    public async Task Details_values_that_are_words_are_plain_and_the_detection_source_is_said_in_words()
    {
        var sheet = await Opened(ClaudeRef);

        var source = sheet.Fields.Single(f => f.Label == Labels.SheetStateSource);
        Assert.Equal("From the session's hook", source.Value);
        Assert.False(source.IsMono);
        Assert.False(sheet.Fields.Single(f => f.Label == "Lane").IsMono);
        Assert.False(sheet.HostFields.Single(f => f.Label == "Shown in").IsMono);
        Assert.True(sheet.Fields.Single(f => f.Label == "Process").IsMono);
        Assert.True(sheet.Fields.Single(f => f.Label == "Working directory").IsMono);
    }

    [Fact]
    public async Task Terminal_is_not_listed_again_when_it_is_the_host()
    {
        var sheet = await Opened(CodexRef);   // Shown in: Warp, Terminal: warp

        Assert.Equal(["Shown in"], sheet.HostFields.Select(f => f.Label), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData(DeepLinkKind.Item, true)]
    [InlineData(DeepLinkKind.Decision, true)]
    [InlineData(DeepLinkKind.Job, true)]
    [InlineData(DeepLinkKind.Node, true)]
    [InlineData(DeepLinkKind.Review, true)]
    [InlineData(DeepLinkKind.Service, false)]
    [InlineData(DeepLinkKind.System, false)]
    [InlineData(DeepLinkKind.Connection, false)]
    [InlineData(DeepLinkKind.Memory, false)]
    [InlineData(DeepLinkKind.Session, false)]
    [InlineData(DeepLinkKind.Project, false)]
    [InlineData(DeepLinkKind.Issue, false)]
    [InlineData(DeepLinkKind.ContextItem, false)]
    [InlineData(DeepLinkKind.Setup, false)]
    public void Sheet_title_is_mono_only_for_ids(DeepLinkKind kind, bool mono) =>
        Assert.Equal(mono, ShellViewModel.TitleIsMonoFor(kind));
}
