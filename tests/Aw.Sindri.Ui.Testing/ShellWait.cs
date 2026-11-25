using System.ComponentModel;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Testing;

/// <summary>Waits for a shell condition by the view models' own change notifications, never by polling: the condition is checked again each time anything the shell hosts reports a change.</summary>
internal static class ShellWait
{
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(20);

    /// <summary>Completes when <paramref name="condition"/> holds; fails after 20 s without it.</summary>
    /// <param name="shell">The shell.</param>
    /// <param name="condition">What to wait for; read on the shell's thread.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the condition holds.</returns>
    internal static Task Until(ShellViewModel shell, Func<bool> condition, CancellationToken cancellationToken) =>
        Wait(shell, run: null, condition, cancellationToken);

    /// <summary>
    /// Completes when <paramref name="condition"/> holds; fails with the run's own exception should the shell's run end first
    /// (a view that throws in the connect pass ends it, and nothing changes after that), else after 20 s without it.
    /// </summary>
    /// <param name="shell">The shell.</param>
    /// <param name="run">The task the shell's Run returned.</param>
    /// <param name="condition">What to wait for; read on the shell's thread.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the condition holds.</returns>
    internal static Task Until(ShellViewModel shell, Task run, Func<bool> condition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        return Wait(shell, run, condition, cancellationToken);
    }

    private static async Task Wait(ShellViewModel shell, Task? run, Func<bool> condition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(condition);
        if (condition())
        {
            return;
        }

        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sources = Sources(shell);
        void check(object? sender, PropertyChangedEventArgs e)
        {
            if (condition())
            {
                _ = held.TrySetResult();
            }
        }

        sources.ForEach(source => source.PropertyChanged += check);
        try
        {
            if (!condition())
            {
                var woke = run is null ? held.Task : Task.WhenAny(held.Task, run);
                await woke.WaitAsync(_bound, cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            sources.ForEach(source => source.PropertyChanged -= check);
        }

        if (!condition())
        {
            await Fail(run).ConfigureAwait(true);
        }
    }

    // The wait woke without the condition: a faulted or cancelled run says why here, at once, not as a 20 s timeout (#4383).
    private static async Task Fail(Task? run)
    {
        if (run is { IsCompleted: true })
        {
            await run.ConfigureAwait(true);
            throw new InvalidOperationException("the shell's run ended before the condition held");
        }

        throw new InvalidOperationException("the condition never held");
    }

    private static List<INotifyPropertyChanged> Sources(ShellViewModel shell) =>
        [shell, shell.Health, shell.Queue, shell.Item, shell.NeedsYou, shell.NeedsYou.Seen, shell.Decision];
}
