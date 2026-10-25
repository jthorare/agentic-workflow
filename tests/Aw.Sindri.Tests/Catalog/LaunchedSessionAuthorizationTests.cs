using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>
/// #1002: a session Sindri launched for a Step reports its own run and nothing a human decides. On the real catalog rows it is refused SND-AUTH-002 on approve,
/// decide, takeover, start and cancel, which an owner-started session keeps (owner decision q93), and it keeps its report, its nonce, its heartbeat and a request.
/// </summary>
public sealed class LaunchedSessionAuthorizationTests
{
    private static readonly CommandCatalog _catalog = new();

    [Theory, Trait("Lane", "unit")]
    [InlineData("ApproveRecipe")]
    [InlineData("Decide")]
    [InlineData("ApplyCommand:takeover")]
    [InlineData("StartInstance")]
    [InlineData("ApplyCommand:cancel")]
    public void A_launched_session_is_refused_a_human_verb_an_owner_started_session_may_run(string action)
    {
        var row = Row(action);

        var launched = Authorization.Decide(row.Class, A.Caller().Launched().Build(), row);
        var owner = Authorization.Decide(row.Class, A.Caller().Session().Build(), row);

        Assert.Equal(SindriErrors.AuthRefused.Code, launched.Refusal?.Code);
        Assert.Null(owner.Refusal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("CompleteStep:report")]
    [InlineData("IssueNonce")]
    [InlineData("SessionHeartbeat")]
    [InlineData("SessionExit")]
    [InlineData("FileRequest")]
    public void A_launched_session_keeps_what_its_own_Step_needs(string action)
    {
        var row = Row(action);

        Assert.Null(Authorization.Decide(row.Class, A.Caller().Launched().Build(), row).Refusal);
    }

    private static CommandRow Row(string action) => _catalog.Rows.Single(r => string.Equals(r.Action.Value, action, StringComparison.Ordinal));
}
