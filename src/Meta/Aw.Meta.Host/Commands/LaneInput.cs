using Aw.Kernel;
using Aw.Meta.Core;

namespace Aw.Meta.Host.Commands;

/// <summary>Reads a command's text from exactly one of --text, --file or (where the command allows it) --stdin.</summary>
internal static class LaneInput
{
    /// <summary>The event of the hook making this call (AW_META_HOOK); null for a call typed by hand.</summary>
    internal static string? HookEvent(CommandContext context) =>
        context.Io.Env("AW_META_HOOK") is { } hook && !string.IsNullOrWhiteSpace(hook) ? hook.Trim() : null;

    internal static async Task<Result<string>> Read(CommandContext context, bool allowStdin)
    {
        var text = context.Line["text"];
        var file = context.Line["file"];
        var stdin = allowStdin && context.Line.Has("stdin");
        if ((text is null ? 0 : 1) + (file is null ? 0 : 1) + (stdin ? 1 : 0) != 1)
        {
            return MetaErrors.Invalid.With(allowStdin ? "give exactly one of --text, --file or --stdin" : "give exactly one of --text or --file");
        }

        if (text is not null)
        {
            return text;
        }

        if (stdin)
        {
            return await context.Io.In.ReadToEndAsync(context.Cancellation).ConfigureAwait(false);
        }

        try
        {
            return await File.ReadAllTextAsync(file!, CancellationToken.None).ConfigureAwait(false);   // why: file is non-null here, the other two choices were handled
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return MetaErrors.Invalid.With($"can't read '{file}': {exception.Message}");
        }
    }
}
