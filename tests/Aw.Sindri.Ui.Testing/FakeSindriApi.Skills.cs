using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The skills read of the fake (skill flows spec §8.1): answers the server's golden.</summary>
internal sealed partial class FakeSindriApi
{
    /// <summary>GET /sindri/skills' answer.</summary>
    public RouteAnswer SkillsAnswer { get; set; } = RouteAnswer.Api("skills", "fixtures-loaded");

    public Task<Result<SkillsView>> Skills(Uri api, CancellationToken cancellationToken) =>
        Task.FromResult(Answer<SkillsView>(api, SindriApiClient.SkillsPath, SkillsAnswer));
}
