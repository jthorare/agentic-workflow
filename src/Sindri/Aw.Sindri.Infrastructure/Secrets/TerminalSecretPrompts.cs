using System.Text;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Infrastructure.Secrets;

/// <summary>ISecretPrompts over the console: the question goes to standard error (standard output carries only the summary), a secret is read key by key with echo off and never written anywhere.</summary>
internal sealed class TerminalSecretPrompts : ISecretPrompts
{
    /// <inheritdoc />
    public bool Interactive => !Console.IsInputRedirected && !Console.IsOutputRedirected && !Console.IsErrorRedirected;

    /// <inheritdoc />
    public async ValueTask<string?> AskSecret(string question, CancellationToken cancellationToken)
    {
        await Console.Error.WriteAsync(question).ConfigureAwait(false);
        var typed = await Raced(static () => ReadSecret(), cancellationToken).ConfigureAwait(false);
        await Console.Error.WriteLineAsync().ConfigureAwait(false);
        return typed;
    }

    /// <summary>
    /// Runs a blocking console read on a background thread and waits for it or the token, whichever comes first. A read blocked in the console can't see a token, and Ctrl-C arrives as a signal rather than a key,
    /// so without the race the first Ctrl-C would be swallowed until the human pressed Enter. The abandoned read dies with the process.
    /// </summary>
    /// <typeparam name="T">What the read returns.</typeparam>
    /// <param name="read">The blocking read.</param>
    /// <param name="cancellationToken">Cancelled by the interrupt.</param>
    /// <returns>What the read returned; throws <see cref="OperationCanceledException"/> when the token fires first.</returns>
    internal static async Task<T> Raced<T>(Func<T> read, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Factory.StartNew(read, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ReadSecret()
    {
        var typed = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                return typed.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (typed.Length > 0)
                {
                    typed.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                _ = typed.Append(key.KeyChar);
            }
        }
    }
}
