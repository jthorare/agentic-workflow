using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>Which credential source a launch uses (sandbox spec SB18).</summary>
public sealed class LoginSourceResolverTests
{
    private static readonly AgentId _claude = AgentId.From("claude-code");
    private static readonly RepoName _repo = RepoName.From("web");

    private static SandboxSettings Settings(ModelLoginSource? profile = null, ModelLoginSource? target = null, ModelLoginSource? repo = null) => new(
        Login: new SandboxLogin(Source: profile, Targets: target is { } t ? new Dictionary<string, ModelLoginSource>(StringComparer.Ordinal) { ["claude-code"] = t } : null),
        Repos: repo is { } r ? new Dictionary<string, SandboxRepoSettings>(StringComparer.Ordinal) { ["web"] = new(Login: new SandboxRepoLogin(Source: r)) } : null);

    [Fact, Trait("Lane", "unit")]
    public void No_settings_resolve_to_owner_copy() => Assert.Equal(ModelLoginSource.OwnerCopy, LoginSourceResolver.Resolve(settings: null, _claude, _repo, stepAsk: null));

    [Fact, Trait("Lane", "unit")]
    public void Repo_override_beats_target_beats_profile()
    {
        Assert.Equal(ModelLoginSource.SeparateLogin, LoginSourceResolver.Resolve(Settings(profile: ModelLoginSource.SeparateLogin), _claude, _repo, stepAsk: null));
        Assert.Equal(ModelLoginSource.OwnerCopy, LoginSourceResolver.Resolve(Settings(profile: ModelLoginSource.SeparateLogin, target: ModelLoginSource.OwnerCopy), _claude, _repo, stepAsk: null));
        Assert.Equal(ModelLoginSource.SeparateLogin, LoginSourceResolver.Resolve(Settings(profile: ModelLoginSource.OwnerCopy, target: ModelLoginSource.OwnerCopy, repo: ModelLoginSource.SeparateLogin), _claude, _repo, stepAsk: null));
        Assert.Equal(ModelLoginSource.OwnerCopy, LoginSourceResolver.Resolve(Settings(profile: ModelLoginSource.SeparateLogin, target: ModelLoginSource.SeparateLogin, repo: ModelLoginSource.OwnerCopy), _claude, _repo, stepAsk: null));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_target_override_for_another_agent_does_not_apply() =>
        Assert.Equal(ModelLoginSource.OwnerCopy, LoginSourceResolver.Resolve(Settings(target: ModelLoginSource.SeparateLogin), AgentId.From("codex"), _repo, stepAsk: null));

    [Fact, Trait("Lane", "unit")]
    public void A_Step_may_ask_for_a_stricter_source() =>
        Assert.Equal(ModelLoginSource.SeparateLogin, LoginSourceResolver.Resolve(Settings(profile: ModelLoginSource.OwnerCopy), _claude, _repo, ModelLoginSource.SeparateLogin));

    [Fact, Trait("Lane", "unit")]
    public void Step_cannot_ask_for_a_looser_source() =>
        Assert.Equal(ModelLoginSource.SeparateLogin, LoginSourceResolver.Resolve(Settings(profile: ModelLoginSource.SeparateLogin), _claude, _repo, ModelLoginSource.OwnerCopy));
}
