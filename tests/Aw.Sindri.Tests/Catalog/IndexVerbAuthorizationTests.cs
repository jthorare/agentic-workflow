using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.RunService;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>Owned-index spec §10, one row per caller rule: 'index sync' (the 'index-sync' service run) is any caller; 'index purge' is human only (SND-AUTH-002 for a session). 'index status' is a query, which has no row and so no caller rule.</summary>
public sealed class IndexVerbAuthorizationTests
{
    private static readonly CommandCatalog _catalog = new();

    [Fact, Trait("Lane", "unit")]
    public void Purge_is_a_terminal_bound_desk_row_with_its_route_and_form()
    {
        var row = ActionClassifier.Classify(_catalog, new PurgeIndexSourceRequest("github"));

        Assert.Equal(("PurgeIndexSource", ActionClass.Desk, "POST", "/sindri/index/purge", "sindri index purge <source>", "cli-only:typed-yes", true, false),
            (row.Action.Value, row.Class, row.Method, row.Route, row.Cli.Text, row.Ui, row.NeedsTerminal, row.SessionOk));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_session_that_sends_purge_is_refused_with_SND_AUTH_002()
    {
        var row = ActionClassifier.Classify(_catalog, new PurgeIndexSourceRequest("github", Confirm: true));

        var refusal = Authorization.Decide(row.Class, A.Caller().Session().Build(), row).Refusal;

        Assert.NotNull(refusal);
        Assert.Equal("SND-AUTH-002", refusal.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_a_human_at_a_terminal_may_send_purge()
    {
        var row = ActionClassifier.Classify(_catalog, new PurgeIndexSourceRequest("github"));

        Assert.Null(Authorization.Decide(row.Class, A.Caller().AtTerminal().Client(ClientLabel.Cli).Build(), row).Refusal);
        Assert.NotNull(Authorization.Decide(row.Class, A.Caller().Human().Client(ClientLabel.Ui).Build(), row).Refusal);
        Assert.NotNull(Authorization.Decide(row.Class, A.Caller().Session().Launched().Build(), row).Refusal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_session_may_start_the_index_sync_service_like_a_human()
    {
        var row = ActionClassifier.Classify(_catalog, new RunServiceRequest(ServiceName.From("index-sync")));

        Assert.Null(Authorization.Decide(row.Class, A.Caller().Session().Build(), row).Refusal);
        Assert.Null(Authorization.Decide(row.Class, A.Caller().AtTerminal().Client(ClientLabel.Cli).Build(), row).Refusal);
    }
}
