using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Features.OnboardRepo;

/// <summary>What an OnboardRepo job did (kept on the job's view; the path is not in it).</summary>
/// <param name="Repo">The repo's name in the profile.</param>
/// <param name="Hooks">What the secret-check hook step said: never a silent green.</param>
/// <param name="Blockers">What the sandbox check found on this box, each in the owner's words (sandbox spec §3.7); null when the check wasn't run.</param>
/// <param name="Offered">One line per toolchain found in the repo's files, saying the entries offered for it; null when there is none.</param>
/// <param name="Snippet">The 'sandbox:' YAML to paste into the profile for the offered entries; null when nothing is offered. Onboarding never writes it: the owner approves profiles.</param>
public sealed record OnboardedRepo(RepoName Repo, string Hooks, IReadOnlyList<SandboxBlockerLine>? Blockers = null, IReadOnlyList<string>? Offered = null, string? Snippet = null);
