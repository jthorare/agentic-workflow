using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The denial counters on disk (sandbox spec §3.6): one file per launch, '&lt;launch dir&gt;/denials/&lt;id&gt;.json', where the counter writes it. A launch whose
/// counter never flushed has no file, so the ledger's 'sandbox.denied' events are the history and these files are the live count. An unreadable file is skipped, not an error.
/// </summary>
/// <param name="launchDirectory">The launcher's directory.</param>
/// <param name="options">The package's AwJson options.</param>
internal sealed class DiskDenialFiles(string launchDirectory, JsonSerializerOptions options) : IDenialFiles
{
    public async ValueTask<IReadOnlyList<DenialFile>> List(LaunchId? launch, CancellationToken cancellationToken)
    {
        string[] paths;
        try
        {
            paths = launch is { } one
                ? [LaunchPaths.DenialsFile(launchDirectory, one)]
                : [.. Directory.EnumerateFiles(LaunchPaths.DenialsDirectory(launchDirectory), "*.json")];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];   // boundary: no denials folder yet means no counter was written
        }

        var found = new List<DenialFile>();
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await Read(path, cancellationToken).ConfigureAwait(false) is { } file)
            {
                found.Add(file);
            }
        }

        return [.. found.OrderByDescending(static f => f.UpdatedAt).ThenBy(static f => f.LaunchId.Value, StringComparer.Ordinal)];
    }

    private async Task<DenialFile?> Read(string path, CancellationToken cancellationToken)
    {
        try
        {
            var counter = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            var parsed = AwJson.Deserialize<DenialFile>(counter, options);
            return parsed.IsSuccess ? parsed.Value : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;   // boundary: a counter that is gone or unreadable has no count to show
        }
    }
}
