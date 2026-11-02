namespace Aw.Context.Features.BuildMap;

/// <summary>What the record sources need from a build.</summary>
/// <param name="Repos">The mapped repos.</param>
/// <param name="DocGlobs">'context.docs.globs'.</param>
/// <param name="DirectoryNames">'context.docs.directoryNames'.</param>
/// <param name="At">The head commit's time: a doc record's age.</param>
internal sealed record MapRecordInput(IReadOnlyList<MapRepoFiles> Repos, IReadOnlyList<string> DocGlobs, IReadOnlyList<string> DirectoryNames, DateTimeOffset At);
