using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Projects page in the shell: it reads only while it is the page shown.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellProjectsTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Shell()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Until(() => shell.State == ShellState.Normal);
        return shell;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition());
    }

    [Fact]
    public Task Going_to_Projects_reads_the_list() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        shell.GoTo(PageId.Projects);
        await Until(() => !shell.Projects.IsLoading);

        Assert.Equal((true, 1), (shell.IsProjects, _server.ProjectsCalls));
        Assert.Equal(3, shell.Projects.Groups.Count);
    }, Token);

    [Fact]
    public Task The_page_reads_nothing_while_another_page_is_shown() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        await Task.Delay(50, Token);

        Assert.Equal(0, _server.ProjectsCalls);
        Assert.True(shell.Projects.IsLoading);
    }, Token);
}
