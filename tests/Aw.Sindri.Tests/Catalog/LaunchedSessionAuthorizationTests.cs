using Aw.Sindri.Core.Commands;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>
/// Agent-first PR 2: a session Sindri launched may pause, decide and run desk actions like an owner-started one. Its report, nonce, heartbeat and requests stay.
/// </summary>
public sealed class LaunchedSessionAuthorizationTests
{
    private static readonly CommandCatalog _catalog = new();

    [Theory, Trait("Lane", "unit")]
    [InlineData("ImportContextMap")]
    [InlineData("Decide")]
    [InlineData("RunWorkflow")]
    [InlineData("CancelWorkflowRun")]
    [InlineData("TakeOverAgentNode")]
    [InlineData("HandBackAgentNode")]
    [InlineData("EndLearnMode")]
    public void A_launched_session_may_decide_pause_and_run_desk_actions(string action)
    {
        var row = Row(action);

        var launched = Authorization.Decide(row.Class, A.Caller().Launched().Build(), row);
        var owner = Authorization.Decide(row.Class, A.Caller().Session().Build(), row);

        Assert.Null(launched.Refusal);
        Assert.Null(owner.Refusal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("CompleteStep:report")]
    [InlineData("SessionHeartbeat")]
    [InlineData("SessionExit")]
    [InlineData("RecordLimitHit")]
    [InlineData("FileRequest")]
    public void A_launched_session_keeps_what_its_own_Step_needs(string action)
    {
        var row = Row(action);

        Assert.Null(Authorization.Decide(row.Class, A.Caller().Launched().Build(), row).Refusal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("TakeOverAgentNode")]
    [InlineData("HandBackAgentNode")]
    public void The_command_list_shows_a_human_and_a_session_for_the_agent_node_rows(string action) =>
        Assert.Equal(["human+terminal", "human", "session"], Authorization.CallersFor(Row(action)));

    private static CommandRow Row(string action) => _catalog.Rows.Single(r => string.Equals(r.Action.Value, action, StringComparison.Ordinal));
}
