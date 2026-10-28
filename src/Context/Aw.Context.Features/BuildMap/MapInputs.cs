using Aw.Context.Core.Extract;

namespace Aw.Context.Features.BuildMap;

/// <summary>What a map build reads besides the profile: the repos' files at their commits, and the record store.</summary>
/// <param name="Repos">Reads each repo at a commit.</param>
/// <param name="Records">The record store's nodes and edges.</param>
internal sealed record MapInputs(IRepoReader Repos, MapRecordSources Records);
