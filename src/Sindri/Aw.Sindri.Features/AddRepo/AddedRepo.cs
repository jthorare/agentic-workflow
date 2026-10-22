using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.AddRepo;

/// <summary>What an AddRepo job did (kept on the job's view; the path is not in it).</summary>
/// <param name="Repo">The repo's name in the profile.</param>
/// <param name="Created">True when the profile file did not exist and this repo created it.</param>
public sealed record AddedRepo(RepoName Repo, bool Created);
