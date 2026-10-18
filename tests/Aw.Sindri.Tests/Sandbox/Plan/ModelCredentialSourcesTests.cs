using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The credential source registry (sandbox spec SB18).</summary>
public sealed class ModelCredentialSourcesTests
{
    private static readonly AgentId _claude = AgentId.From("claude-code");
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('a', 32));

    [Fact, Trait("Lane", "unit")]
    public void The_registry_is_keyed_by_id_and_a_duplicate_id_is_a_bug()
    {
        var registry = new ModelCredentialSources([new FakeModelCredentialSource(ModelLoginSource.OwnerCopy)]);

        Assert.True(registry.Has(ModelLoginSource.OwnerCopy));
        Assert.False(registry.Has(ModelLoginSource.SeparateLogin));
        _ = Assert.Throws<ArgumentException>(() => new ModelCredentialSources([new FakeModelCredentialSource(ModelLoginSource.OwnerCopy), new FakeModelCredentialSource(ModelLoginSource.OwnerCopy)]));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_with_no_adapter_is_017_source_unsupported_with_its_id()
    {
        var registry = new ModelCredentialSources([new FakeModelCredentialSource(ModelLoginSource.OwnerCopy).With(_claude, LoginState.Ready)]);

        var prepared = await registry.Prepare(ModelLoginSource.SeparateLogin, _claude, _launch, TestContext.Current.CancellationToken);
        var checkedStatus = registry.Check(ModelLoginSource.SeparateLogin, _claude);

        foreach (var error in new[] { prepared.Error, checkedStatus.Error })
        {
            Assert.Equal(SindriErrors.LaunchLoginUnavailable.Code, error.Code);
            Assert.Equal("source-unsupported", SandboxRefusal.ReasonOf(error));
            Assert.Equal("separate-login", SandboxRefusal.SourceTextOf(error));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Unavailable_source_is_017_with_its_source_id_and_never_falls_back()
    {
        var owner = new FakeModelCredentialSource(ModelLoginSource.OwnerCopy).With(_claude, LoginState.Ready);
        var separate = new FakeModelCredentialSource(ModelLoginSource.SeparateLogin).With(_claude, LoginState.Expired);
        var registry = new ModelCredentialSources([owner, separate]);

        var result = await registry.Prepare(ModelLoginSource.SeparateLogin, _claude, _launch, TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.LaunchLoginUnavailable.Code, result.Error.Code);
        Assert.Equal("expired", SandboxRefusal.ReasonOf(result.Error));
        Assert.Equal("separate-login", SandboxRefusal.SourceTextOf(result.Error));
        Assert.Empty(owner.Prepared);
    }

    [Fact, Trait("Lane", "unit")]
    public void Check_reports_ready_or_the_reason()
    {
        var registry = new ModelCredentialSources([new FakeModelCredentialSource(ModelLoginSource.OwnerCopy).With(_claude, LoginState.Ready).With(AgentId.From("codex"), LoginState.Missing)]);

        Assert.True(registry.Check(ModelLoginSource.OwnerCopy, _claude).IsSuccess);
        Assert.Equal("no-login", SandboxRefusal.ReasonOf(registry.Check(ModelLoginSource.OwnerCopy, AgentId.From("codex")).Error));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Release_goes_to_the_source_that_prepared_the_login()
    {
        var owner = new FakeModelCredentialSource(ModelLoginSource.OwnerCopy).With(_claude, LoginState.Ready);
        var registry = new ModelCredentialSources([owner]);
        var prepared = (await registry.Prepare(ModelLoginSource.OwnerCopy, _claude, _launch, TestContext.Current.CancellationToken)).Value;

        registry.Release(prepared);
        registry.Release(prepared with { Source = ModelLoginSource.SeparateLogin });   // no adapter: nothing to do, no throw

        Assert.Equal([prepared.Handle], owner.Released);
    }
}
