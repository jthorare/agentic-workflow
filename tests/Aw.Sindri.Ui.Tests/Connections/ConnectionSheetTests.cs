using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Connections;

namespace Aw.Sindri.Ui.Tests.Connections;

/// <summary>The connection sheet (the GET route): one connection with its actions, read on open and again while the sheet is open.</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionSheetTests
{
    private readonly ConnectionsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ConnectionSheetViewModel Sheet() => new(_f.Server, _f.Launcher, _f.Clock);

    [Fact]
    public async Task Opening_reads_the_connection_and_names_it()
    {
        _f.SeedThree();
        var sheet = Sheet();
        sheet.Show("notes:mcp");

        Assert.Equal((FramePhase.Loading, "notes:mcp"), (sheet.Phase, sheet.Heading));

        await sheet.Open(ConnectionsFixture.Url, "notes:mcp", Token);

        Assert.Equal((FramePhase.Shown, "notes:mcp"), (sheet.Phase, sheet.Heading));
        Assert.Equal(("! Needs sign-in", true), (sheet.Row!.ChipText, sheet.Row.CanSignIn));
        Assert.Equal(Labels.ConnectionSecretLine(has: true), sheet.SecretLine);
    }

    [Fact]
    public async Task A_connection_Sindri_does_not_know_says_so()
    {
        var sheet = Sheet();
        sheet.Show("nope:none");

        await sheet.Open(ConnectionsFixture.Url, "nope:none", Token);

        Assert.Equal((FramePhase.NotFound, Labels.ConnectionUnknown), (sheet.Phase, sheet.ProblemText));
        Assert.Null(sheet.Row);
    }

    [Fact]
    public async Task Closing_the_sheet_stops_its_sign_in_wait_and_frees_the_row()
    {
        _f.SeedThree();
        var sheet = Sheet();
        sheet.Show("notes:mcp");
        await sheet.Open(ConnectionsFixture.Url, "notes:mcp", Token);
        var row = sheet.Row!;
        var run = row.SignIn(Token);
        await _f.Drive(Task.Delay(30, Token));   // some polls, no callback
        Assert.False(run.IsCompleted);

        sheet.Close();
        await run.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);   // real time: the fake clock never runs out on its own

        Assert.Equal((false, string.Empty), (row.IsBusy, row.NoteText));
    }

    [Fact]
    public async Task Removing_from_the_sheet_closes_it_on_the_API_confirm()
    {
        _f.SeedThree();
        var sheet = Sheet();
        sheet.Show("acme:key");
        await sheet.Open(ConnectionsFixture.Url, "acme:key", Token);
        var removed = false;
        sheet.Removed += (_, _) => removed = true;

        sheet.Row!.AskRemove();
        await sheet.Row.ConfirmRemove(Token);

        Assert.True(removed);
        Assert.Contains("remove acme:key", _f.Server.ConnectionCallLog, StringComparer.Ordinal);
    }
}
