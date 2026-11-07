using Aw.Sindri.Core;
using Aw.Sindri.Features.GetLibrary;
using Aw.Sindri.Features.GetLibraryDoc;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri library …' (PR review page spec 2026-11-04 §5.2, §5.5). <c language="text">sync</c> is 'sindri index sync library' under the name the Library page and the doctor row use: the same check that the profile has
/// the feed, the same job (POST /sindri/index/sync with the source 'library'), the same exit codes (0 synced, 1 the source ended with a code or Sindri is not running, 2 invalid).
/// <c language="text">list</c> and <c language="text">show</c> read GET /sindri/library and GET /sindri/library/{doc} and never start the control plane (exit 0 ok, 1 not running, 2 anything else, SND-LIB-404 included);
/// <c language="text">validate</c> runs the rules over a file or folder in this process.
/// </summary>
/// <param name="index">The index verbs it runs through.</param>
/// <param name="client">The API client.</param>
/// <param name="output">Where a usage error is written.</param>
internal sealed class LibraryVerbs(IndexVerbs index, ControlPlaneClient client, Output output)
{
    internal const string Usage = "library sync [--detach] [--json] [--no-start] | library list [--errors|--near-misses] [--json] | library show <doc> [--json] | " + LibraryValidate.Usage;

    private static readonly string[] _syncFlags = ["--detach", "--json", "--no-start"];

    internal async Task<int> Run(string[] args, CancellationToken interrupt, CancellationToken again)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args switch
        {
            ["sync", .. var flags] when flags.All(static f => _syncFlags.Contains(f, StringComparer.Ordinal)) => await index.Run(["sync", "library", .. flags], interrupt, again).ConfigureAwait(false),
            ["list", .. var flags] => await List(flags, interrupt).ConfigureAwait(false),
            ["show", .. var rest] => await Show(rest, interrupt).ConfigureAwait(false),
            ["validate", .. var rest] => await new LibraryValidate(output).Run(rest, interrupt).ConfigureAwait(false),
            _ => await output.Error(SindriErrors.InvalidInput.With($"'library {string.Join(' ', args)}' isn't a library command: {Usage}")).ConfigureAwait(false),
        };
    }

    private async Task<int> List(string[] flags, CancellationToken ct)
    {
        var json = flags.Contains("--json", StringComparer.Ordinal);
        var errors = flags.Contains("--errors", StringComparer.Ordinal);
        var nearMisses = flags.Contains("--near-misses", StringComparer.Ordinal);
        if (flags.Any(static f => f is not ("--json" or "--errors" or "--near-misses")) || (errors && nearMisses))
        {
            return await output.Error(SindriErrors.InvalidInput.With("library list takes --json and one of --errors or --near-misses: library list [--errors|--near-misses] [--json]")).ConfigureAwait(false);
        }

        var answer = await client.Get<GetLibraryResponse>("/sindri/library", ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        return json
            ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(LibraryText.List(answer.Value.Value, errors ? LibraryText.Part.Errors : nearMisses ? LibraryText.Part.NearMisses : LibraryText.Part.All), 0).ConfigureAwait(false);
    }

    private async Task<int> Show(string[] args, CancellationToken ct)
    {
        var json = args.Contains("--json", StringComparer.Ordinal);
        if (args.Where(static a => a != "--json").ToArray() is not [{ Length: > 0 } doc] || doc.StartsWith('-'))
        {
            return await output.Error(SindriErrors.InvalidInput.With("which doc? library show <doc> [--json]; `sindri library list` names them")).ConfigureAwait(false);
        }

        var answer = await client.Get<GetLibraryDocResponse>("/sindri/library/" + Uri.EscapeDataString(doc), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        return json
            ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(LibraryText.Doc(answer.Value.Value.Doc), 0).ConfigureAwait(false);
    }
}
