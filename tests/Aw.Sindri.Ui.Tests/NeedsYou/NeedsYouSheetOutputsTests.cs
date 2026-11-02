using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The sheet of a desk task's Needs-you row (#1170, ux-A-1c): the same file inputs as the instance frame, and its Complete sends the file's contents instead of the row's "open it with Answer" refusal.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouSheetOutputsTests
{
    private const string Key = "fact:output";

    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeFilePicker _picker = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The task the sheet reads: the dev-desk-round-trip golden, whose desk requires fact:output and whose complete command names the outcome "done".
    public NeedsYouSheetOutputsTests()
    {
        _server.InstanceBody = _ => new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"));
    }

    private async Task<(ShellViewModel Shell, NeedsYouRowViewModel Row)> OpenSheet()
    {
        _records.Write(A.Record());
        var window = new WindowPorts(NullLauncher.Instance, _picker, NullFolderPicker.Instance);
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard(), window: window);
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal, Token);
        var entry = NeedsYouRows.Desk();
        shell.ShowNeedsYou(new NeedsYouFeed([entry], ""));
        shell.NeedsYou.OpenSheetOn(entry.Id);
        var row = shell.NeedsYou.Row(entry.Id)!;
        await ShellWait.Until(shell, () => row.HasOutputs, Token);
        return (shell, row);
    }

    [Fact]
    public Task The_sheet_of_a_desk_row_asks_for_the_required_output_and_Complete_waits_for_it() => UiThread.Run(async () =>
    {
        var (_, row) = await OpenSheet();

        Assert.Equal(Key, Assert.Single(row.Outputs).Key);
        Assert.Equal((false, Labels.PickOutput(Key)), (row.CanComplete, row.OutputHint));
    }, Token);

    [Fact]
    public Task Complete_sends_the_chosen_files_contents_under_the_outputs_key() => UiThread.Run(async () =>
    {
        _picker.Queue(new PickedFile("export.txt", 3, "abc"u8.ToArray()));
        var (shell, row) = await OpenSheet();
        await row.Outputs[0].Browse(Token);

        await shell.CompleteSheetRow(row);

        var (_, body) = Assert.Single(_server.CompleteCalls);
        Assert.Equal("abc"u8.ToArray(), body.Outputs[Key]);
        Assert.Equal((true, Labels.CompleteSent), (row.CanComplete, row.ActionNote));
    }, Token);

    [Fact]
    public Task Complete_without_the_file_sends_nothing() => UiThread.Run(async () =>
    {
        var (shell, row) = await OpenSheet();

        await shell.CompleteSheetRow(row);

        Assert.Empty(_server.CompleteCalls);
    }, Token);
}
