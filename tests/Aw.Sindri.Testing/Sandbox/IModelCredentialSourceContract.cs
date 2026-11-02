using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Xunit;

namespace Aw.Sindri.Testing.Sandbox;

/// <summary>
/// The contract every <see cref="IModelCredentialSource"/> honours (sandbox spec SB18), run against the fake and every adapter
/// (OwnerCopy in SB-2, SeparateLogin in SB-8).
/// </summary>
public abstract class IModelCredentialSourceContract
{
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('c', 32));

    /// <summary>The target every scenario is about; the source supports it.</summary>
    protected static AgentId Target { get; } = AgentId.From("claude-code");

    /// <summary>A target the source has no adapter for.</summary>
    protected static AgentId Unsupported { get; } = AgentId.From("cursor");

    /// <summary>A source whose login for <see cref="Target"/> is in <paramref name="state"/>.</summary>
    /// <param name="state">Ready, missing or expired.</param>
    /// <returns>The source.</returns>
    protected abstract IModelCredentialSource Create(LoginState state);

    /// <summary>The one-line fact every adapter shares: a ready login prepares, names this source and is released.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Credential_source_contract_holds_for_every_adapter()
    {
        var source = Create(LoginState.Ready);

        var prepared = await source.Prepare(Target, _launch, TestContext.Current.CancellationToken);
        source.Release(prepared.Value.Handle);

        Expect.True(source.Supports(Target) && prepared.IsSuccess && prepared.Value.Source == source.Id);
    }

    /// <summary>The source supports its target and not another.</summary>
    [Fact]
    public void Supports_its_target_and_not_an_unknown_one()
    {
        var source = Create(LoginState.Ready);

        Expect.True(source.Supports(Target));
        Expect.True(!source.Supports(Unsupported));
    }

    /// <summary>A probe reports the state and names its own source, and asking twice changes nothing.</summary>
    [Theory]
    [InlineData(LoginState.Ready)]
    [InlineData(LoginState.Missing)]
    [InlineData(LoginState.Expired)]
    public void Probe_reports_the_state_and_its_own_source_without_a_side_effect(LoginState state)
    {
        var source = Create(state);

        var first = source.Probe(Target);
        var second = source.Probe(Target);

        Expect.Equal(state, first.State);
        Expect.Equal(source.Id, first.Source);
        Expect.Equal(first, second);
    }

    /// <summary>A ready login prepares into plain data the plan builder consumes.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Prepare_of_a_ready_login_is_plain_data_for_the_plan_builder()
    {
        var source = Create(LoginState.Ready);

        var result = await source.Prepare(Target, _launch, TestContext.Current.CancellationToken);

        Expect.True(result.IsSuccess);
        var prepared = result.Value;
        Expect.Equal(source.Id, prepared.Source);
        Expect.True(prepared.Mounts.Count > 0);
        Expect.True(!string.IsNullOrWhiteSpace(prepared.Handle.Value));
        Expect.True(prepared.DeniedReads.Count > 0);
        Expect.True(EnvironmentAllowlist.Violations(prepared.Environment.Keys).Count == 0);
        string[] renameWritten = [".credentials.json", ".claude.json", "auth.json"];
        Expect.True(!prepared.Mounts.Any(m => renameWritten.Any(f => m.SandboxPath.EndsWith("/" + f, StringComparison.Ordinal))));   // bound by directory only
    }

    /// <summary>A login that is not ready is SND-LAUNCH-017 with the reason and this source's id.</summary>
    /// <param name="state">Missing or expired.</param>
    /// <param name="reason">The reason.</param>
    /// <returns>The test.</returns>
    [Theory]
    [InlineData(LoginState.Missing, "no-login")]
    [InlineData(LoginState.Expired, "expired")]
    public async Task Prepare_of_a_login_that_is_not_ready_is_017_with_the_reason_and_the_source(LoginState state, string reason)
    {
        var source = Create(state);

        var result = await source.Prepare(Target, _launch, TestContext.Current.CancellationToken);

        Expect.True(result.IsError);
        Expect.Equal(SindriErrors.LaunchLoginUnavailable.Code, result.Error.Code);
        Expect.Equal(reason, SandboxRefusal.ReasonOf(result.Error));
        Expect.Equal(source.Id == ModelLoginSource.OwnerCopy ? "owner-copy" : "separate-login", SandboxRefusal.SourceTextOf(result.Error));
    }

    /// <summary>A target the source has no adapter for is SND-LAUNCH-017 source-unsupported.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Prepare_for_an_unsupported_target_is_017_source_unsupported()
    {
        var source = Create(LoginState.Ready);

        var result = await source.Prepare(Unsupported, _launch, TestContext.Current.CancellationToken);

        Expect.True(result.IsError);
        Expect.Equal(SindriErrors.LaunchLoginUnavailable.Code, result.Error.Code);
        Expect.Equal("source-unsupported", SandboxRefusal.ReasonOf(result.Error));
    }

    /// <summary>Two prepares for two launches give two handles, and releasing is safe to repeat.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Prepares_are_per_launch_and_release_is_idempotent()
    {
        var source = Create(LoginState.Ready);
        var other = LaunchId.From("LCH-" + new string('d', 32));

        var a = (await source.Prepare(Target, _launch, TestContext.Current.CancellationToken)).Value;
        var b = (await source.Prepare(Target, other, TestContext.Current.CancellationToken)).Value;

        Expect.True(a.Handle != b.Handle);
        source.Release(a.Handle);
        source.Release(a.Handle);
        source.Release(new LoginHandle("never-prepared"));
    }
}
