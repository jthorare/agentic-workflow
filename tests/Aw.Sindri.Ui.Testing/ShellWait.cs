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
    internal static async Task Until(ShellViewModel shell, Func<bool> condition, CancellationToken cancellationToken)
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
                await held.Task.WaitAsync(_bound, cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            sources.ForEach(source => source.PropertyChanged -= check);
        }

        if (!condition())
        {
            throw new InvalidOperationException("the condition never held");
        }
    }

    private static List<INotifyPropertyChanged> Sources(ShellViewModel shell) =>
        [shell, shell.Health, shell.Queue, shell.Item, shell.NeedsYou, shell.NeedsYou.Seen, shell.Decision];
}
