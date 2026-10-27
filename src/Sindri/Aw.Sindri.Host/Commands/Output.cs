using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>Where a command writes: text or JSON on stdout, errors as 'CODE message' on stderr. No colour, so state is always a word.</summary>
internal sealed class Output(TextWriter stdout, TextWriter stderr, JsonSerializerOptions options)
{
    /// <summary>The shell's exit code for a command ended by SIGINT.</summary>
    internal const int InterruptedExit = 130;

    public TextWriter Out { get; } = stdout;

    /// <summary>The same output with standard output bound to standard error: text a --json run may print without breaking its one document.</summary>
    /// <returns>An output whose text and prompts go to standard error.</returns>
    public Output OnStandardError() => new(stderr, stderr, options);

    /// <summary>Reads a UTF-8 JSON document the way the API's bodies are read (camelCase names, strict).</summary>
    /// <param name="utf8Json">The document.</param>
    /// <typeparam name="T">A type in the package's JSON context.</typeparam>
    /// <returns>The value, or the reason it can't be read.</returns>
    public Result<T> Read<T>(ReadOnlySpan<byte> utf8Json) => AwJson.Deserialize<T>(utf8Json, options);

    public async Task<int> Json<T>(T value, int exitCode)
    {
        await Out.WriteLineAsync(Encoding.UTF8.GetString(AwJson.Serialize(value, options))).ConfigureAwait(false);
        return exitCode;
    }

    /// <summary>Prints <paramref name="value"/> as its JSON document with one more member, <paramref name="name"/>, added at the end.</summary>
    /// <param name="value">The value, a type in the package's JSON context that serializes to an object.</param>
    /// <param name="name">The member to add.</param>
    /// <param name="member">The member's value.</param>
    /// <param name="exitCode">The exit code to return.</param>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <returns><paramref name="exitCode"/>.</returns>
    public async Task<int> JsonWith<T>(T value, string name, JsonNode member, int exitCode)
    {
        var document = JsonNode.Parse(AwJson.Serialize(value, options))!.AsObject();   // why: the bytes were just serialized from an object
        document[name] = member;
        await Out.WriteLineAsync(document.ToJsonString(options)).ConfigureAwait(false);
        return exitCode;
    }

    /// <summary>Prints an API response body exactly as received, then a newline (--json, API spec §9).</summary>
    /// <param name="body">The body.</param>
    /// <param name="exitCode">The exit code to return.</param>
    /// <returns><paramref name="exitCode"/>.</returns>
    public async Task<int> Raw(ReadOnlyMemory<byte> body, int exitCode)
    {
        await Out.WriteLineAsync(Encoding.UTF8.GetString(body.Span)).ConfigureAwait(false);
        return exitCode;
    }

    /// <summary>Prints one progress line on standard error, so a --json run's one document on standard output stays whole.</summary>
    /// <param name="line">The line.</param>
    /// <returns>When it is written.</returns>
    public Task Notice(string line) => stderr.WriteLineAsync(line);

    public async Task<int> Text(string text, int exitCode)
    {
        await Out.WriteAsync(text).ConfigureAwait(false);
        return exitCode;
    }

    /// <summary>
    /// Prints the error; exit 1 for 'not running' (attention), 2 for everything else. 'Not running' gets the fix
    /// 'sindri' unless it names one already.
    /// </summary>
    public async Task<int> Error(Error error)
    {
        var notRunning = error.Code == SindriErrors.NotRunning.Code;
        var named = error.Message.Contains("fix:", StringComparison.Ordinal) || error.Message.Contains("ask the human", StringComparison.Ordinal);
        var fix = notRunning && !named ? " (fix: sindri)" : string.Empty;
        await stderr.WriteLineAsync($"{error.Code} {error.Message}{fix}").ConfigureAwait(false);
        return notRunning ? 1 : 2;
    }
}
