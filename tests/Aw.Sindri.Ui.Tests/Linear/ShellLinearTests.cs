using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>The Linear view in the shell: L on Work, the C key, Settings from the Connect card, and reads only while Work is shown.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellLinearTests
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
    public Task L_toggles_the_view_on_Work_and_does_nothing_elsewhere() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        var elsewhere = shell.Press("L");
        shell.GoTo(PageId.Work);
        var onWork = shell.Press("L");

        Assert.Equal((false, true, true), (elsewhere, onWork, shell.Linear.IsLinear));
    }, Token);

    [Fact]
    public Task C_opens_the_create_form_on_the_Sindri_view_and_does_nothing_yet_on_the_Linear_view() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.GoTo(PageId.Work);

        shell.Linear.Choose(linear: true);
        var onLinear = shell.Press("C");
        var openOnLinear = shell.Create.IsOpen;
        shell.Linear.Choose(linear: false);
        var onSindri = shell.Press("C");

        Assert.Equal((true, false, true, true), (onLinear, openOnLinear, onSindri, shell.Create.IsOpen));
    }, Token);

    [Fact]
    public Task Going_to_Work_on_the_Linear_view_reads_the_list_and_leaving_stops_reading() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.Linear.Choose(linear: true);
        await Task.Delay(50, Token);
        Assert.Empty(_server.LinearQueries);

        shell.GoTo(PageId.Work);
        await Until(() => !shell.Linear.IsLoading);

        Assert.Equal(["mine", "teams"], _server.LinearQueries.Select(q => q.Group).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }, Token);

    [Fact]
    public Task The_Connect_Linear_button_goes_to_Settings() => UiThread.Run(async () =>
    {
        _server.LinearIssuesAnswer = RouteAnswer.Api("linear-issues", "not-connected");
        var shell = await Shell();
        shell.GoTo(PageId.Work);
        shell.Linear.Choose(linear: true);
        await Until(() => shell.Linear.ShowsConnect);

        shell.Linear.RequestConnect();

        Assert.True(shell.IsSettings);
    }, Token);
}
