using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.AddRepo;

/// <summary>
/// Add a repo to the profile from the app (setup-once spec §5.1, POST /setup/repos; a desk action, so a session is refused): a job keyed by the first 16 hex of the sha256 of the resolved path, so a second add of the same
/// path while one runs is SND-JOB-409. The cheap checks (the folder, the name) run before the job exists and answer SND-SETUP-001 or 002.
/// </summary>
/// <param name="Path">The absolute path of the folder the owner picked.</param>
/// <param name="Name">The repo's name; null takes the folder's name, normalised to the repo-name rules.</param>
public sealed record AddRepoRequest(string Path, string? Name = null) : IJob<AddedRepo>;
