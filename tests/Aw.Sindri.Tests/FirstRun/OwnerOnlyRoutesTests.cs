using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.ConnectAccount;
using Aw.Sindri.Features.RestartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.FirstRun;

/// <summary>RestartProcess and ConnectAccount are the owner's (first-run spec decisions 11 and 13): a caller without the per-start human token, the launched session first, gets SND-AUTH-002, as `profile set` does.</summary>
public sealed class OwnerOnlyRoutesTests
{
    private readonly SliceHarness _slice = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_launched_session_cannot_restart_Sindri()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Launched().Build());

        var result = await new RestartProcessService(_slice.Callers).Handle(new RestartProcessRequest(), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_without_the_human_token_cannot_restart_Sindri()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Session().Build());

        var result = await new RestartProcessService(_slice.Callers).Handle(new RestartProcessRequest(), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("github")]
    [InlineData("linear")]
    [InlineData("slack")]
    public async Task A_launched_session_cannot_connect_an_account(string provider)
    {
        using var entered = _slice.Callers.Enter(A.Caller().Launched().Build());

        var result = await new ConnectAccountService(_slice.Callers).Handle(new ConnectAccountRequest(provider), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_without_the_human_token_cannot_connect_an_account()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Session().Build());

        var result = await new ConnectAccountService(_slice.Callers).Handle(new ConnectAccountRequest("github"), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_owner_with_the_human_token_is_not_refused()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());

        var restart = await Record.ExceptionAsync(async () => await new RestartProcessService(_slice.Callers).Handle(new RestartProcessRequest(), Token));
        var connect = await Record.ExceptionAsync(async () => await new ConnectAccountService(_slice.Callers).Handle(new ConnectAccountRequest("github"), Token));

        _ = Assert.IsType<NotSupportedException>(restart);   // past the check, the stub is unbuilt
        _ = Assert.IsType<NotSupportedException>(connect);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_caller_with_the_token_and_no_launch_marker_is_human_and_one_with_a_launch_marker_is_a_session()
    {
        var owner = CallerHeaderParsing.Parse("human", terminal: null, client: null, repo: null, launch: null);
        var launched = CallerHeaderParsing.Parse("human", terminal: null, client: null, repo: null, launch: "LCH-00000000000000000000000000000001");

        Assert.Equal((CallerClass.Human, null), (owner.Value.Class, owner.Value.Launch));   // an agent the owner started, with the token: not refused
        Assert.Equal(CallerClass.Session, launched.Value.Class);
        _ = Assert.NotNull(launched.Value.Launch);
    }
}
