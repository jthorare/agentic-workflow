using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The skills read (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <summary>The path of GET /sindri/skills.</summary>
    internal const string SkillsPath = "/sindri/skills";

    /// <inheritdoc />
    public Task<Result<SkillsView>> Skills(Uri api, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
