namespace Aw.Sindri.Host;

/// <summary>A map build in progress, as the "no map yet" answer says it; every member the answer leaves out is null.</summary>
/// <param name="JobId">The build job.</param>
/// <param name="StartedAt">When it started.</param>
/// <param name="SourcesDone">How many of the context sources have synced since it started.</param>
/// <param name="SourcesTotal">How many context sources there are.</param>
/// <param name="FilesDone">How many files of the current repo the build has read.</param>
/// <param name="FilesTotal">How many files the build reads in the current repo.</param>
/// <param name="LastSyncAt">When the context sources last synced.</param>
internal sealed record ApiProblemBuilding(string? JobId = null, DateTimeOffset? StartedAt = null, int? SourcesDone = null, int? SourcesTotal = null, DateTimeOffset? LastSyncAt = null, int? FilesDone = null, int? FilesTotal = null);
