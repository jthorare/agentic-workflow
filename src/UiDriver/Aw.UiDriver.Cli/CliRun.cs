using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.UiDriver.Core;

namespace Aw.UiDriver.Cli;

/// <summary>
/// The command: parse the arguments, find the driver (--dir, else AW_UI_DRIVER_DIR), send the verb and print the answer on standard output. A failure is one
/// line on standard error, 'aw-ui-driver: AW-UIDRIVER-NNN message'. Exit: 0 done · 1 usage or an invalid request · 2 no element, the element can't take the
/// action, or a wait timed out (a check failed) · 4 the driver is unreachable or the app failed.
/// </summary>
/// <param name="output">Standard output.</param>
/// <param name="error">Standard error.</param>
/// <param name="environment">Reads an environment variable.</param>
internal sealed class CliRun(TextWriter output, TextWriter error, Func<string, string?> environment)
{
    private static readonly TimeSpan _poll = TimeSpan.FromMilliseconds(100);

    internal async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        if (CliArguments.Parse(args) is not { } a)
        {
            await error.WriteLineAsync("aw-ui-driver: usage: " + CliArguments.Usage).ConfigureAwait(false);
            return 1;
        }

        var directory = a.Value("--dir") ?? environment("AW_UI_DRIVER_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return await FailAsync(UiDriverErrors.Invalid.With("no driver directory: pass --dir or set AW_UI_DRIVER_DIR")).ConfigureAwait(false);
        }

        return a.Verb == "ready" ? await ReadyAsync(directory, a, cancellationToken).ConfigureAwait(false) : await CallAsync(directory, a, cancellationToken).ConfigureAwait(false);
    }

    // Waits until a driver answers (the app may still be starting), up to --wait-ms (default 30000).
    private async Task<int> ReadyAsync(string directory, CliArguments a, CancellationToken cancellationToken)
    {
        var limit = TimeSpan.FromMilliseconds(a.Number("--wait-ms") ?? 30_000);
        var started = TimeProvider.System.GetTimestamp();
        var last = UiDriverErrors.Unreachable.With($"no driver answered in {directory}");
        while (TimeProvider.System.GetElapsedTime(started) < limit)
        {
            var opened = await DriverClient.OpenAsync(directory, cancellationToken).ConfigureAwait(false);
            if (opened.IsSuccess)
            {
                using var client = opened.Value;
                if (await client.PingAsync(cancellationToken).ConfigureAwait(false))
                {
                    await output.WriteLineAsync("{\"ok\":true}").ConfigureAwait(false);
                    return 0;
                }
            }
            else
            {
                last = opened.Error;
            }

            await Task.Delay(_poll, cancellationToken).ConfigureAwait(false);
        }

        return await FailAsync(last).ConfigureAwait(false);
    }

    private async Task<int> CallAsync(string directory, CliArguments a, CancellationToken cancellationToken)
    {
        var request = Requests.For(a);
        if (request.IsError)
        {
            return await FailAsync(request.Error).ConfigureAwait(false);
        }

        var opened = await DriverClient.OpenAsync(directory, cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return await FailAsync(opened.Error).ConfigureAwait(false);
        }

        using var client = opened.Value;
        var answer = await client.CallAsync(request.Value.Verb, request.Value.Body, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await FailAsync(answer.Error).ConfigureAwait(false);
        }

        if (request.Value.Verb != "screenshot")
        {
            await output.WriteLineAsync(Encoding.UTF8.GetString(answer.Value)).ConfigureAwait(false);
            return 0;
        }

        return await SavePictureAsync(a, answer.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> SavePictureAsync(CliArguments a, byte[] png, CancellationToken cancellationToken)
    {
        if (a.Value("--out") is not { } path)
        {
            await error.WriteLineAsync("aw-ui-driver: a screenshot needs --out <file.png>").ConfigureAwait(false);
            return 1;
        }

        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            await File.WriteAllBytesAsync(path, png, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return await FailAsync(UiDriverErrors.Failed.With($"{path} was not written: {e.Message}")).ConfigureAwait(false);
        }

        await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{{\"path\":\"{path.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\",\"bytes\":{png.Length}}}")).ConfigureAwait(false);
        return 0;
    }

    private async Task<int> FailAsync(Error failure)
    {
        await error.WriteLineAsync($"aw-ui-driver: {failure.Code} {failure.Message.ReplaceLineEndings(" ")}").ConfigureAwait(false);
        return failure.Kind switch
        {
            ErrorKind.Invalid => 1,
            ErrorKind.NotFound or ErrorKind.Timeout => 2,
            ErrorKind.Conflict or ErrorKind.RateLimited or ErrorKind.BudgetExceeded or ErrorKind.Unavailable or ErrorKind.Forbidden => 4,
            _ => 4,
        };
    }
}
