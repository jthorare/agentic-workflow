using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="ILedgerSignal"/> honours (API spec §5.5): a pulse past a waiter's cursor wakes it at
/// once, one at or before the cursor doesn't, a wait with no pulse ends at its limit with the latest known sequence, and
/// one pulse wakes every waiter.
/// </summary>
public abstract class LedgerSignalContract
{
    private static TimeSpan Limit => TimeSpan.FromSeconds(1);

    /// <summary>The contract's clock: waits end on it.</summary>
    protected FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A fresh signal over <paramref name="clock"/>.</summary>
    /// <param name="clock">The only clock.</param>
    /// <returns>The signal.</returns>
    protected abstract ILedgerSignal Create(TimeProvider clock);

    /// <summary>A waiter at cursor 3 wakes on a pulse of 4 without the clock moving.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_pulse_past_the_cursor_wakes_a_waiter_at_once()
    {
        var signal = Create(Clock);
        var wait = signal.WaitAfter(3, Limit, Token).AsTask();
        Expect.True(!wait.IsCompleted);

        signal.Pulse(4);

        Expect.Equal(4L, await wait.WaitAsync(TimeSpan.FromSeconds(5), Token));
    }

    /// <summary>A pulse at the cursor leaves the waiter waiting until its limit; a pulse already past the cursor returns at once.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_pulse_at_or_before_the_cursor_doesnt()
    {
        var signal = Create(Clock);
        var wait = signal.WaitAfter(5, Limit, Token).AsTask();

        signal.Pulse(5);
        Expect.True(!wait.IsCompleted);
        Clock.Advance(Limit);

        Expect.Equal(5L, await wait.WaitAsync(TimeSpan.FromSeconds(5), Token));
        Expect.Equal(5L, await signal.WaitAfter(4, Limit, Token).AsTask().WaitAsync(TimeSpan.FromSeconds(5), Token));
    }

    /// <summary>With no pulse, a wait ends when the clock reaches its limit, with the latest known sequence (0 before any pulse).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Without_a_pulse_the_wait_ends_at_the_limit()
    {
        var signal = Create(Clock);
        var wait = signal.WaitAfter(0, Limit, Token).AsTask();

        Clock.Advance(Limit - TimeSpan.FromMilliseconds(1));
        Expect.True(!wait.IsCompleted);
        Clock.Advance(TimeSpan.FromMilliseconds(1));

        Expect.Equal(0L, await wait.WaitAsync(TimeSpan.FromSeconds(5), Token));
    }

    /// <summary>Every waiter behind the pulse wakes on it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Many_waiters_wake_on_one_pulse()
    {
        var signal = Create(Clock);
        var waits = Enumerable.Range(0, 3).Select(i => signal.WaitAfter(i, Limit, Token).AsTask()).ToList();

        signal.Pulse(7);

        Expect.Sequence([7L, 7L, 7L], await Task.WhenAll(waits).WaitAsync(TimeSpan.FromSeconds(5), Token));
    }
}
