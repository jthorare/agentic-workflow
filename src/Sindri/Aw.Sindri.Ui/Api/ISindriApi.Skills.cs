using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The skills read (<see cref="ISindriApi"/>, continued; skill flows spec §8.1).</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/skills: the skill blocks Sindri loaded.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The blocks, or SND-UI-002/003.</returns>
    public Task<Result<SkillsView>> Skills(Uri api, CancellationToken cancellationToken);
}
