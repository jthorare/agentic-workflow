using System.Text;
using Aw.Kernel;
using Aw.Redaction;
using Aw.Transcripts.Core;
using Aw.Transcripts.Infrastructure.Files;

namespace Aw.Transcripts.Infrastructure.Reading;

/// <summary>
/// Streams one transcript file line by line through its provider's <see cref="ILineReader"/> (files can be over 100 MB,
/// so a file is never read into one string). Turn text goes through the secret scrubber before it is kept. A transcript
/// is untrusted data: it is parsed, never followed.
/// </summary>
/// <param name="redactor">The secret scrubber.</param>
internal sealed class TranscriptFileReader(IRedactor redactor)
{
    public async ValueTask<Result<ParsedFile>> Read(TranscriptFile file, CancellationToken cancellationToken)
    {
        var parsed = new ParsedFile();
        var reader = LineReader(file);
        try
        {
            var stream = new FileStream(file.Path, new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.Read, Share = FileShare.ReadWrite | FileShare.Delete, Options = FileOptions.SequentialScan | FileOptions.Asynchronous });
            await using (stream.ConfigureAwait(false))
            {
                using var text = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
                var number = 0;
                while (await text.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
                {
                    number++;
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        reader.Read(number, line, parsed);
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return TranscriptsErrors.Unreadable.With($"{file.SessionOf().Value}: {exception.GetType().Name}");
        }

        return parsed;
    }

    private ILineReader LineReader(TranscriptFile file) => file.Provider switch
    {
        TranscriptProvider.ClaudeCode => new ClaudeLineReader(file.IsSubagentByPlace, file.ModifiedAt, Scrub),
        TranscriptProvider.Codex => new CodexLineReader(file.ModifiedAt, Scrub),
        TranscriptProvider.Cursor => new CursorLineReader(file.IsSubagentByPlace, file.ModifiedAt, Scrub),
        _ => throw new ArgumentOutOfRangeException(nameof(file), file.Provider, "not a transcript provider"),
    };

    private string Scrub(string text) => redactor.Scrub(text).Text;
}
