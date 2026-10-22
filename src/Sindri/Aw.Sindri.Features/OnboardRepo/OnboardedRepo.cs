using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.OnboardRepo;

/// <summary>What an OnboardRepo job did (kept on the job's view; the path is not in it).</summary>
/// <param name="Repo">The repo's name in the profile.</param>
/// <param name="Hooks">What the secret-check hook step said: never a silent green.</param>
public sealed record OnboardedRepo(RepoName Repo, string Hooks);
