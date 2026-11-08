using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The Linear card in the shell: leaving Settings drops the click's own answer, so reopening it shows what is true then, with or without an event in between.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellLinearConnectionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Leaving_Settings_with_no_event_drops_the_Saved_prefix_of_a_write_key_of_unknown_scope()
    {
        var records = new InMemoryEndpointRecords();
        records.Write(A.Record());
        var server = new FakeSindriApi
        {
            Catalog = RouteAnswer.Api("commands", "catalog"),
            QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting"),
            NeedsYouView = RouteAnswer.Api("needs-you", "empty"),
            WorkflowAnswer = RouteAnswer.Api("workflow-instances", "empty"),
            LinearConnectAnswer = RouteAnswer.Api("linear-connect", "write-scope-unknown"),
            LinearConnectionAnswer = RouteAnswer.Api("linear-connection", "scope-unknown"),
        };
        var shell = new ShellViewModel(records, server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        for (var i = 0; i < 400 && shell.Health.ApiUrl() is null; i++)
        {
            await Task.Delay(25, Token);
        }

        shell.GoTo(PageId.Settings);
        shell.LinearConnection.WriteKey = "lin_api_write";
        await shell.LinearConnection.CheckWrite(Token);
        var saved = shell.LinearConnection.AttentionText;
        shell.GoTo(PageId.Home);

        Assert.Equal((Labels.LinearConnScopeUnknown, Labels.LinearConnScopeUnknownRead), (saved, shell.LinearConnection.AttentionText));
    }
}
