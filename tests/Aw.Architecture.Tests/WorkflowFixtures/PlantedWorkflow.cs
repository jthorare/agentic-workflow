using System.Diagnostics;
using System.Runtime.CompilerServices;
using Temporalio.Workflows;

#pragma warning disable RS0030 // why: this fixture plants the wall-clock, Guid and Thread.Sleep calls ARCH-WF-001 must find; it is read as IL and never run

namespace Aw.Architecture.Tests.WorkflowFixtures;

/// <summary>A workflow type with one banned call per member (an ARCH-WF-001 fixture read as IL, never run). The attribute sits on this part only; the other part is PlantedWorkflow.Part.cs.</summary>
[Workflow]
internal static partial class PlantedWorkflow
{
    internal static async Task CancelAsyncCall(CancellationTokenSource source) => await source.CancelAsync().ConfigureAwait(true);

    internal static void CancelAfterCall(CancellationTokenSource source) => source.CancelAfter(TimeSpan.FromSeconds(1));

    internal static bool TimeSpanCtor()
    {
        using var source = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        return source.IsCancellationRequested;
    }

    internal static bool IntCtor()
    {
        using var source = new CancellationTokenSource(1000);
        return source.IsCancellationRequested;
    }

    internal static Task TaskRunCall(CancellationToken token) => Task.Run(static () => { }, token);

    internal static Task TaskDelayCall(CancellationToken token) => Task.Delay(TimeSpan.FromSeconds(1), token);

    internal static Task WhenAnyCall(Task<int> first, Task<int> second) => Task.WhenAny(first, second);

    internal static Task WhenAllCall(Task first, Task second) => Task.WhenAll(first, second);

    internal static async Task ConfigureAwaitFalse(Task work) => await work.ConfigureAwait(false);

    internal static Func<Task> LambdaDelay(CancellationToken token) => () => Task.Delay(1, token);

    internal static YieldAwaitable TaskYield() => Task.Yield();

    internal static Task StartNewCall(CancellationToken token) => Task.Factory.StartNew(static () => { }, token, TaskCreationOptions.None, TaskScheduler.Default);

    internal static Task WaitAsyncTimeout(Task work, CancellationToken token) => work.WaitAsync(TimeSpan.FromSeconds(1), token);

    internal static DateTime DateTimeNow() => DateTime.Now;

    internal static DateTime DateTimeUtcNow() => DateTime.UtcNow;

    internal static DateTimeOffset DateTimeOffsetNow() => DateTimeOffset.Now;

    internal static DateTimeOffset DateTimeOffsetUtcNow() => DateTimeOffset.UtcNow;

    internal static Guid NewGuidCall() => Guid.NewGuid();

    internal static Random NewRandom() => new();

    internal static Random SharedRandom() => Random.Shared;

    internal static void SleepCall() => Thread.Sleep(1);

    internal static Stopwatch StopwatchCall() => Stopwatch.StartNew();

    internal static TimeProvider SystemTimeProvider() => TimeProvider.System;

    internal static int TickCount() => Environment.TickCount;

    internal static ParallelLoopResult ParallelFor() => Parallel.For(0, 1, static _ => { });

    internal static System.Threading.Timer ThreadingTimer() => new(callback: static _ => { }, state: null, dueTime: 0, period: 0);

    internal static Task UsesHelper(CancellationToken token) => Helper.Wait(token);

    /// <summary>A type nested in the workflow type: its members count as the workflow's.</summary>
    public static class Helper
    {
        /// <summary>Waits on the wrong scheduler.</summary>
        /// <param name="token">The token.</param>
        /// <returns>The delay.</returns>
        public static Task Wait(CancellationToken token) => Task.Delay(1, token);
    }
}
