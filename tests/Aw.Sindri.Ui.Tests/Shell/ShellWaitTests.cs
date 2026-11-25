using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// ShellWait given the shell's run (#4383): a follow that throws (a view that throws in the connect pass) ends the run, and nothing
/// bumps the shell after that, so a wait that only listens for changes sat out its whole 20 s bound and said "timed out".
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ShellWaitTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ShellViewModel Shell() =>
        new(new InMemoryEndpointRecords(), new FakeSindriApi(), new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());

    [Fact]
    public Task A_run_that_throws_fails_the_wait_with_its_own_exception() => UiThread.Run(async () =>
    {
        var run = Task.FromException(new InvalidOperationException("a followed view threw"));

        var thrown = await Record.ExceptionAsync(() => ShellWait.Until(Shell(), run, static () => false, Token));

        Assert.Equal((typeof(InvalidOperationException), "a followed view threw"), (thrown?.GetType(), thrown?.Message));
    }, Token);

    [Fact]
    public Task A_run_that_ends_before_the_condition_fails_the_wait_at_once() => UiThread.Run(async () =>
    {
        var thrown = await Record.ExceptionAsync(() => ShellWait.Until(Shell(), Task.CompletedTask, static () => false, Token));

        Assert.Equal((typeof(InvalidOperationException), "the shell's run ended before the condition held"), (thrown?.GetType(), thrown?.Message));
    }, Token);
}
