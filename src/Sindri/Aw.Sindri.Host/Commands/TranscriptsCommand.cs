using Aw.Sindri.Core;
using Aw.Sindri.Host.Commands.Transcripts;
using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Ports;
using Aw.Transcripts.Core.Refs;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri transcripts list|show': the local agent transcripts (Aw.Transcripts) read in this process. It reads files only,
/// needs no control plane and never starts serve, so an agent session may run it. Exit 0 ok, 1 an unknown ref or an
/// unreadable file, 2 usage.
/// </summary>
internal static class TranscriptsCommand
{
    /// <summary>Runs the verb.</summary>
    /// <param name="args">The arguments after 'transcripts'.</param>
    /// <param name="services">The container, for the corpus and the clock.</param>
    /// <param name="output">Where the answer goes.</param>
    /// <param name="fence">Whether turn text is fenced as untrusted: true unless a person runs the CLI on a terminal.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The exit code.</returns>
    internal static async Task<int> Run(string[] args, IServiceProvider services, Output output, bool fence, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var corpus = services.GetRequiredService<ITranscriptCorpus>();
        return args switch
        {
            ["list", .. var rest] => await List(rest, corpus, services.GetRequiredService<TimeProvider>().GetUtcNow(), output, ct).ConfigureAwait(false),
            ["show", var text] when !text.StartsWith('-') => await Show(text, (Json: false, Fence: fence), (corpus, output), ct).ConfigureAwait(false),
            ["show", var text, "--json"] when !text.StartsWith('-') => await Show(text, (Json: true, Fence: fence), (corpus, output), ct).ConfigureAwait(false),
            ["show", "--json", var text] when !text.StartsWith('-') => await Show(text, (Json: true, Fence: fence), (corpus, output), ct).ConfigureAwait(false),
            ["show", ..] => await output.Error(SindriErrors.InvalidInput.With($"transcripts show needs one ref ({TranscriptsArguments.ShowUsage})")).ConfigureAwait(false),
            _ => await output.Error(SindriErrors.InvalidInput.With($"transcripts takes list or show ({TranscriptsArguments.ListUsage}; {TranscriptsArguments.ShowUsage})")).ConfigureAwait(false),
        };
    }

    /// <summary>Whether turn text reaches this caller fenced as untrusted.</summary>
    /// <param name="session">How the CLI classed itself.</param>
    /// <returns>True unless a person runs it on a terminal: a session Sindri launched or started is never a person, whatever its streams are (#3466).</returns>
    internal static bool Fenced(AgentSession session) => !session.Terminal || session.Launch is not null || session.OneOff;

    private static async Task<int> List(string[] args, ITranscriptCorpus corpus, DateTimeOffset now, Output output, CancellationToken ct)
    {
        var parsed = TranscriptsArguments.List(args, now);
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var (query, json) = parsed.Value;
        var listed = await corpus.ListSessions(query, ct).ConfigureAwait(false);
        if (listed.IsError)
        {
            return await Failed(listed.Error, output).ConfigureAwait(false);
        }

        return json
            ? await output.Json(new TranscriptListView(query.Since, Fenced: false, [.. listed.Value.Select(TranscriptsText.View)]), 0).ConfigureAwait(false)
            : await output.Text(TranscriptsText.List(listed.Value, query.Since), 0).ConfigureAwait(false);
    }

    private static async Task<int> Show(string text, (bool Json, bool Fence) how, (ITranscriptCorpus Corpus, Output Output) io, CancellationToken ct)
    {
        var (json, fence) = how;
        var (corpus, output) = io;
        if (!SessionRef.TryFrom(text, out var session))
        {
            return await output.Error(SindriErrors.InvalidInput.With($"'{text}' is not a session ref: it is 'transcript:' and 8 hex digits, as 'transcripts list' prints ({TranscriptsArguments.ShowUsage})")).ConfigureAwait(false);
        }

        var read = await corpus.ReadSession(session, ct).ConfigureAwait(false);
        if (read.IsError)
        {
            return await Failed(read.Error, output).ConfigureAwait(false);
        }

        return json
            ? await output.Json(TranscriptsText.View(read.Value, fence), 0).ConfigureAwait(false)
            : await output.Text(TranscriptsText.Show(read.Value, fence), 0).ConfigureAwait(false);
    }

    // An unknown ref, an unreadable file or two files with one ref: attention (exit 1), not a usage error.
    private static async Task<int> Failed(Aw.Kernel.Error error, Output output)
    {
        await output.Notice($"{error.Code} {error.Message}").ConfigureAwait(false);
        return error.Code == TranscriptsErrors.Invalid.Code ? 2 : 1;
    }
}
