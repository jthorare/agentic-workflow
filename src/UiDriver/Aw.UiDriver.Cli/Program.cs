namespace Aw.UiDriver.Cli;

/// <summary>The entry point: arguments in, one command, an exit code out. Ctrl-C cancels the call.</summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var cancel = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancel.Cancel();
        };
        try
        {
            return await new CliRun(Console.Out, Console.Error, Environment.GetEnvironmentVariable).RunAsync(args, cancel.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("aw-ui-driver: interrupted").ConfigureAwait(false);
            return 130;
        }
    }
}
