using Aw.Sindri.Core.Control;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IShutdown"/> honours: Stopping is live until a stop is requested, a request cancels it at
/// once, a second request is harmless, and it stays cancelled. Run against the fake and the process's own shutdown.
/// </summary>
public abstract class ShutdownContract
{
    /// <summary>A fresh shutdown; the test disposes it when it is disposable.</summary>
    /// <returns>The shutdown.</returns>
    protected abstract IShutdown Create();

    /// <summary>Nothing is cancelled before a request.</summary>
    [Fact]
    public void Stopping_is_live_until_a_stop_is_requested()
    {
        var shutdown = Create();
        using var owned = shutdown as IDisposable;

        Expect.True(!shutdown.Stopping.IsCancellationRequested);
    }

    /// <summary>A request cancels Stopping, a second one is harmless, and it stays cancelled.</summary>
    [Fact]
    public void A_request_cancels_Stopping_and_a_second_one_is_harmless()
    {
        var shutdown = Create();
        using var owned = shutdown as IDisposable;
        var token = shutdown.Stopping;

        shutdown.Request();
        shutdown.Request();

        Expect.True(token.IsCancellationRequested);
        Expect.True(shutdown.Stopping.IsCancellationRequested);
    }
}
