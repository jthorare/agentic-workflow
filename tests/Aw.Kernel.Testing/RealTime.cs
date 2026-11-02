namespace Aw.Kernel.Testing;

/// <summary>
/// The wall clock, fresh GUIDs and real sleeps, for the few places a test genuinely needs them: a deadline when it polls
/// a real process or server, a file's modified time, a unique temp name. Everything else takes a 'FakeTimeProvider' and
/// a fake 'IIdGenerator'. Test code never calls 'DateTime.UtcNow', 'Guid.NewGuid' or 'Thread.Sleep' itself
/// (BannedSymbols.Tests.txt), so every use of real time is named here and easy to find.
/// </summary>
public static class RealTime
{
    /// <summary>Gets the current UTC time as a 'DateTime'.</summary>
    public static DateTime NowDateTime =>
#pragma warning disable RS0030 // this class is the one place tests read the wall clock
            DateTime.UtcNow;
#pragma warning restore RS0030


    /// <summary>Gets the current UTC time as a 'DateTimeOffset'.</summary>
    public static DateTimeOffset NowOffset =>
#pragma warning disable RS0030 // this class is the one place tests read the wall clock
            DateTimeOffset.UtcNow;
#pragma warning restore RS0030


    /// <summary>Gets the system clock as a 'TimeProvider', for an adapter under test that times a real process.</summary>
    public static TimeProvider Clock =>
#pragma warning disable RS0030 // this class is the one place tests take the system time provider
        TimeProvider.System;
#pragma warning restore RS0030

    /// <summary>A fresh GUID, for a name that must not clash (a temp folder, a secret); never an id under test.</summary>
    /// <returns>The GUID.</returns>
    public static Guid NewGuid() =>
#pragma warning disable RS0030 // this class is the one place tests make a random GUID
        Guid.NewGuid();
#pragma warning restore RS0030


    /// <summary>Blocks the calling thread for real time, for code under test that has no signal to wait on.</summary>
    /// <param name="duration">How long.</param>
    public static void Sleep(TimeSpan duration) =>
#pragma warning disable RS0030 // this class is the one place tests sleep
        Thread.Sleep(duration);
#pragma warning restore RS0030

}
