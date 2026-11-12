using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;
using static Aw.Sindri.Ui.Tests.Agents.Page.AgentsPageFixture;

namespace Aw.Sindri.Ui.Tests.Agents.Page;

/// <summary>What a row calls an agent and where it runs (#2811): a name a person recognises, never a process id first; the group and the host in plain words.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentsPageNamingTests
{
    private readonly AgentsPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SessionRowViewModel RowOf(AgentsPageViewModel page, string prefix) =>
        page.Groups.SelectMany(g => g.Rows).Single(r => r.Ref.StartsWith(prefix, StringComparison.Ordinal));

    [Fact]
    public async Task Title_falls_back_to_folder_not_pid()
    {
        var list = Edited(_ => { });
        var claude = Row(list, "claude-code/");
        claude["title"] = null;
        claude["id"] = "pid-411-99";
        var codex = Row(list, "codex/");
        codex["repo"] = null;
        codex["worktree"] = "work/ideas";
        _f.Serve(list);

        var page = await _f.Shown(Token);

        Assert.Equal("api/ideas", RowOf(page, "claude-code/").TitleText);   // repo, then its worktree folder
        Assert.Equal("ideas", RowOf(page, "codex/").TitleText);             // no repo: the worktree folder
        Assert.Equal("pid-333", RowOf(page, "cursor/").TitleText);          // nothing else known: the pid, last
    }

    [Fact]
    public async Task Title_falls_back_to_the_repo_alone_when_there_is_no_worktree()
    {
        var page = await _f.Shown(Token);

        Assert.Equal("web", RowOf(page, "codex/").TitleText);
    }

    [Fact]
    public async Task Group_is_named_Started_by_you()
    {
        var page = await _f.Shown(Token);

        Assert.Equal("Started by you", page.Groups[1].Name);
    }

    [Fact]
    public async Task Where_it_runs_is_capitalised_when_the_host_is_unknown()
    {
        var page = await _f.Shown(Token);

        Assert.StartsWith("Unknown", RowOf(page, "cursor/").WhereText, StringComparison.Ordinal);
    }
}
