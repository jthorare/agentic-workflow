using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Features.BuildMap;

/// <summary>The record half of the build: the store read once, its nodes and edges joined to the extraction.</summary>
internal sealed partial class BuildMapService
{
    // The record store read once for the whole build (spec 2026-10-27 §7.1). A store or a checkout that can't be read is the one degraded line 'records: unavailable (<code>)' and no record nodes: the build is about the code, and the next build reads them again.
    private async ValueTask<(ExtractedMap Map, IReadOnlyList<string> Degraded, IReadOnlyList<string> Notes)> Records(ContextSettings settings, List<(ContextRepo Repo, RepoExtraction Read)> extractions, ExtractedMap found, CancellationToken cancellationToken)
    {
        var at = clock.GetUtcNow();
        var files = new List<MapRepoFiles>(extractions.Count);
        var directoryDocs = new List<DirectoryDoc>();
        foreach (var (repo, read) in extractions)
        {
            var paths = read.Paths.Where(p => !repo.Denies(p)).ToList();
            var nodes = DocRecords.Find(repo.Name, paths, settings.DocGlobs, settings.DocDirectoryNames, at);
            var docs = nodes.Select(n => n.Label).ToList();
            directoryDocs.AddRange(RecordRollup.DirectoryDocs(repo.Name, nodes, settings.DocDirectoryNames, read.Directories));
            var blobs = await inputs.Repos.BlobIds(repo, read.Commit, docs, cancellationToken).ConfigureAwait(false);
            if (blobs.IsError)
            {
                return (ExtractedMap.Empty, [Unavailable(blobs.Error)], []);
            }

            files.Add(new MapRepoFiles(repo.Name, paths, blobs.Value));
        }

        var reading = await inputs.Records.Read(new MapRecordInput(files, settings.DocGlobs, settings.DocDirectoryNames, at), cancellationToken).ConfigureAwait(false);
        return reading.IsError
            ? (ExtractedMap.Empty, [Unavailable(reading.Error)], [])
            : (reading.Value.Map.Plus(RecordRollup.Edges([.. reading.Value.Overflow, .. reading.Value.UnderCap], directoryDocs, found)), reading.Value.Degraded, [.. reading.Value.Notes.Except(reading.Value.Degraded, StringComparer.Ordinal)]);
    }

    private static string Unavailable(Error error) => $"records: unavailable ({error.Code})";
}
