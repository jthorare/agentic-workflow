using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri library sync [--detach] [--json] [--no-start]' (PR review page spec 2026-11-04 §5.2): the library feed's tick now. It is 'sindri index sync library' under the name the Library page and the doctor row use:
/// the same check that the profile has the feed, the same job (POST /sindri/index/sync with the source 'library'), the same exit codes (0 synced, 1 the source ended with a code or Sindri is not running, 2 invalid).
/// </summary>
/// <param name="index">The index verbs it runs through.</param>
/// <param name="output">Where a usage error is written.</param>
internal sealed class LibraryVerbs(IndexVerbs index, Output output)
{
    internal const string Usage = "library sync [--detach] [--json] [--no-start]";

    private static readonly string[] _flags = ["--detach", "--json", "--no-start"];

    internal async Task<int> Run(string[] args, CancellationToken interrupt, CancellationToken again)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args is ["sync", .. var flags] && flags.All(static f => _flags.Contains(f, StringComparer.Ordinal))
            ? await index.Run(["sync", "library", .. flags], interrupt, again).ConfigureAwait(false)
            : await output.Error(SindriErrors.InvalidInput.With($"'library {string.Join(' ', args)}' isn't a library command: {Usage}")).ConfigureAwait(false);
    }
}
