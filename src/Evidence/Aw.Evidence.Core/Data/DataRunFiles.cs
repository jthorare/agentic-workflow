using System.Text;
using System.Text.Json;
using Aw.Evidence.Core.Files;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Evidence.Core.Data;

/// <summary>The data-evidence run's own files: the script it reads, the goldens it compares and the scrubbed summary it writes.</summary>
/// <param name="files">The run file port.</param>
/// <param name="scrubber">The pattern scrubber.</param>
/// <param name="json">The package's AwJson options.</param>
public sealed class DataRunFiles(IRunFiles files, ITextScrubber scrubber, JsonSerializerOptions json)
{
    /// <summary>The summary's file name inside the run directory.</summary>
    public const string SummaryFile = "summary.json";

    /// <summary>Reads a file.</summary>
    /// <param name="path">The file (absolute).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes, or the file port's error.</returns>
    public ValueTask<Result<byte[]>> ReadAsync(string path, CancellationToken cancellationToken) => files.ReadAsync(path, cancellationToken);

    /// <summary>Reads a database script, naming the file in a failure.</summary>
    /// <param name="path">The script's path, for the message.</param>
    /// <param name="bytes">The script's bytes.</param>
    /// <returns>The script, or AW-EVIDENCE-018.</returns>
    public Result<DbScript> ParseDb(string path, ReadOnlySpan<byte> bytes) =>
        DbScriptReader.Read(bytes, json).Match<Result<DbScript>>(script => script, error => error with { Message = $"script {path}: {error.Message}" });

    /// <summary>Reads an API script, naming the file in a failure.</summary>
    /// <param name="path">The script's path, for the message.</param>
    /// <param name="bytes">The script's bytes.</param>
    /// <returns>The script, or AW-EVIDENCE-017.</returns>
    public Result<ApiScript> Parse(string path, ReadOnlySpan<byte> bytes) =>
        ApiScriptReader.Read(bytes, json).Match<Result<ApiScript>>(script => script, error => error with { Message = $"script {path}: {error.Message}" });

    /// <summary>The scrubber for one run: the pattern scrubber, and each header value and passed-through value replaced by value first.</summary>
    /// <param name="headerValues">Header values the run resolved that must never be recorded.</param>
    /// <param name="envValues">Passed-through variable values (and other resolved secrets) that must never be recorded.</param>
    /// <returns>The scrubber.</returns>
    public ITextScrubber ScrubberFor(IEnumerable<string> headerValues, IEnumerable<string> envValues) => new ValueScrubber(scrubber, headerValues, envValues);

    /// <summary>A refusal or error message with its text scrubbed (the message is free text; its code is not).</summary>
    /// <param name="message">The message, or null.</param>
    /// <param name="runScrubber">The run's scrubber.</param>
    /// <returns>The message scrubbed, or null.</returns>
    public static CodedMessage? Messaged(CodedMessage? message, ITextScrubber runScrubber) => message is null ? null : message with { Message = runScrubber.Scrub(message.Message) };

    /// <summary>Makes the run directory and deletes an earlier summary in it, so a run that stops early never leaves a stale one.</summary>
    /// <param name="runDirectory">The run directory (absolute).</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Success, or the file port's error.</returns>
    public async ValueTask<Result<Unit>> PrepareRunDirectoryAsync(string runDirectory, CancellationToken cancellationToken)
    {
        var created = await files.CreateDirectoryAsync(runDirectory, cancellationToken).ConfigureAwait(false);
        return created.IsError ? created : await files.DeleteAsync(RunPath.Combine(runDirectory, SummaryFile), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a run summary as &lt;run-dir&gt;/summary.json, then reads it back. The free-text fields were scrubbed as they were recorded and the refusal and error messages
    /// are scrubbed by the caller (<see cref="Messaged"/>). Fixed fields (host, outcome, names, ids) are never value-scrubbed: a secret that happens to equal one would corrupt the file.
    /// </summary>
    /// <param name="runDirectory">The run directory (absolute).</param>
    /// <param name="summary">The summary.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <typeparam name="TSummary">The summary type, known to the package's JSON context.</typeparam>
    /// <returns>The summary as read back and the exact document written, or an error.</returns>
    public async ValueTask<Result<(TSummary Summary, string Document)>> WriteDocumentAsync<TSummary>(string runDirectory, TSummary summary, CancellationToken cancellationToken)
        where TSummary : notnull
    {
        var document = Encoding.UTF8.GetString(AwJson.Serialize(summary, json));
        var written = await files.WriteAsync(RunPath.Combine(runDirectory, SummaryFile), Encoding.UTF8.GetBytes(document), cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error;
        }

        return AwJson.Deserialize<TSummary>(Encoding.UTF8.GetBytes(document), json).Map(read => (read, document));
    }
}
