using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// The command line's streams: a command produces an exit code, stdout and stderr; each stream gets
/// its text plus "\n" only when the text isn't empty, as UTF-8. Stdin is read whole and deserialized into the command's
/// input record. A test captures all three for its own async flow (<see cref="Capture"/>).
/// </summary>
internal static class CommandOutput
{
    private static readonly AsyncLocal<CommandCapture?> _capture = new();

    /// <summary>Captures stdin, stdout and stderr for the calling async flow, instead of the process's streams.</summary>
    internal static CommandCapture Capture(string stdin) => _capture.Value = new CommandCapture(stdin);

    /// <summary>Writes the frozen output and returns the exit code.</summary>
    internal static int Emit(int exitCode, string stdout, string stderr = "")
    {
        Write(stdout, error: false);
        Write(stderr, error: true);
        return exitCode;
    }

    /// <summary>Reads stdin to the end and deserializes it into <typeparamref name="T"/> through the AwJson profile.</summary>
    /// <returns>The record, or AW-KERNEL-001 when stdin isn't JSON or misses the record's shape (required, nullable).</returns>
    internal static async Task<Result<T>> ReadInput<T>(JsonSerializerOptions json, CancellationToken cancellationToken)
    {
        if (_capture.Value is { } capture)
        {
            return AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(capture.Stdin), json);
        }

        var stdin = Console.OpenStandardInput();
        await using (stdin.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            await stdin.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            return AwJson.Deserialize<T>(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), json);
        }
    }

    /// <summary>Reads stdin to the end as UTF-8 text (a question's input, which the evaluator itself deserializes).</summary>
    internal static async Task<string> ReadText(CancellationToken cancellationToken)
    {
        if (_capture.Value is { } capture)
        {
            return capture.Stdin;
        }

        var stdin = Console.OpenStandardInput();
        await using (stdin.ConfigureAwait(false))
        {
            using var reader = new StreamReader(stdin, Encoding.UTF8);
            return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>A value as Kernel-profile JSON (the context's records carry the contract's field names).</summary>
    internal static string Json<T>(T value, JsonSerializerOptions json) => Encoding.UTF8.GetString(AwJson.Serialize(value, json));

    /// <summary>Stdin that isn't the command's input record: exit 1, 'invalid input: &lt;reason&gt;' on one line.</summary>
    internal static int InvalidInput(string reason) => Emit(1, "", $"invalid input: {OneLine(reason)}");

    /// <summary>The scaffold's mapping (/new-slice generates calls to it): the value as Kernel JSON on stdout, or exit 1
    /// and the error's message as one stderr line.</summary>
    internal static int Write<T>(Result<T> result, JsonSerializerOptions json) =>
        result.IsError
            ? Emit(1, "", OneLine(result.Error.Message))
            : Emit(0, Json(result.Value, json));

    /// <summary>A usage error: exit 64 and one stderr line, 'usage: judge &lt;synopsis&gt;' and the problem in parentheses.</summary>
    /// <param name="synopsis">The command's synopsis after 'judge ', e.g. 'why &lt;decision-id&gt;'.</param>
    /// <param name="problem">What was wrong with the arguments, or null.</param>
    internal static int Usage(string synopsis, string? problem = null) =>
        Emit(UsageExit, "", problem is null ? $"usage: judge {synopsis}" : $"usage: judge {synopsis} ({OneLine(problem)})");

    /// <summary>The usage exit code (BSD sysexits EX_USAGE).</summary>
    internal const int UsageExit = 64;

    // Error text is one stderr line: any line break inside it becomes a space.
    private static string OneLine(string text) => text.ReplaceLineEndings(" ");

    private static void Write(string text, bool error)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (_capture.Value is { } capture)
        {
            _ = (error ? capture.Stderr : capture.Stdout).Append(text).Append('\n');
            return;
        }

        using var stream = error ? Console.OpenStandardError() : Console.OpenStandardOutput();
        stream.Write(Encoding.UTF8.GetBytes(text + "\n"));
        stream.Flush();
    }
}
