using Aw.Kernel;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Toolkits;

/// <summary>
/// Logs every toolkit call (agent-first PR 16): each payload goes through the scrubber (Aw.Redaction) first and is capped after, so a secret
/// can't survive by straddling the cap.
/// </summary>
/// <param name="calls">The call store.</param>
/// <param name="scrubber">The scrubber over Aw.Redaction.</param>
public sealed class ToolkitCallLog(IToolkitCalls calls, ITextScrubber scrubber)
{
    /// <summary>The most characters of an input or an output a row keeps (16 KB of text).</summary>
    public const int MaxPayloadChars = 16 * 1024;

    private readonly IToolkitCalls _calls = calls;
    private readonly ITextScrubber _scrubber = scrubber;

    /// <summary>Cuts a text to <see cref="MaxPayloadChars"/> characters, never splitting a surrogate pair.</summary>
    /// <param name="text">The text, or null.</param>
    /// <param name="truncated">True when text was cut.</param>
    /// <returns>The text or its first characters.</returns>
    public static string? Cap(string? text, out bool truncated)
    {
        truncated = false;
        if (text is null || text.Length <= MaxPayloadChars)
        {
            return text;
        }

        truncated = true;

        var length = char.IsHighSurrogate(text[MaxPayloadChars - 1]) ? MaxPayloadChars - 1 : MaxPayloadChars;
        return text[..length];
    }

    /// <summary>Scrubs, caps and stores one call.</summary>
    /// <param name="entry">The raw call.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit.</returns>
    public ValueTask<Result<Unit>> Record(ToolkitCallEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var input = Cap(Scrub(entry.Input), out var inputCut);
        var output = Cap(Scrub(entry.Output), out var outputCut);
        var call = new ToolkitCall(entry.Toolkit, entry.Tool, entry.Caller, input, output, inputCut || outputCut, Scrub(entry.Error), (long)entry.Duration.TotalMilliseconds, entry.StartedAt);
        return _calls.Add(call, cancellationToken);
    }

    private string? Scrub(string? text) => text is null ? null : _scrubber.Scrub(text);
}
