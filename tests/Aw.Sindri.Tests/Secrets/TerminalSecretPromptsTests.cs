using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Secrets;

namespace Aw.Sindri.Tests.Secrets;

// A console read blocks and can't see a token: the first Ctrl-C must still end the wait for Enter and the password prompt.
[Lane(TestLane.Unit)]
public sealed class TerminalSecretPromptsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_cancelled_token_ends_a_blocked_read_without_waiting_for_it()
    {
        using var release = new ManualResetEventSlim();
        using var source = new CancellationTokenSource();
        var waiting = TerminalSecretPrompts.Raced(() =>
        {
            release.Wait(Token);
            return "typed";
        }, source.Token);

        await source.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        release.Set();   // let the abandoned read end
    }

    [Fact]
    public async Task A_read_that_finishes_first_returns_its_value()
        => Assert.Equal("typed", await TerminalSecretPrompts.Raced(static () => "typed", Token));

    [Fact]
    public async Task A_token_already_cancelled_reads_nothing()
    {
        var read = false;
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TerminalSecretPrompts.Raced(() =>
        {
            read = true;
            return "x";
        }, source.Token));

        Assert.False(read);
    }
}
