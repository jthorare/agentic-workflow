using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.RunService;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>Owned-index spec §10, one row per caller rule: 'index sync' (the 'index-sync' service run) is any caller; 'index purge' is a desk row any caller may run (the CLI still asks a typed yes). 'index status' is a query, which has no row and so no caller rule.</summary>
public sealed class IndexVerbAuthorizationTests
{
    private static readonly CommandCatalog _catalog = new();

    [Fact, Trait("Lane", "unit")]
    public void Purge_is_a_desk_row_with_its_route_and_form_and_no_terminal_need()
    {
        var row = ActionClassifier.Classify(_catalog, new PurgeIndexSourceRequest("github"));

        Assert.Equal(("PurgeIndexSource", ActionClass.Desk, "POST", "/sindri/index/purge", "sindri index purge <source>", "cli-only:typed-yes", false, false),
            (row.Action.Value, row.Class, row.Method, row.Route, row.Cli.Text, row.Ui, row.NeedsTerminal, row.SessionOk));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_session_may_send_purge()
    {
        var row = ActionClassifier.Classify(_catalog, new PurgeIndexSourceRequest("github", Confirm: true));

        Assert.Null(Authorization.Decide(row.Class, A.Caller().Session().Build(), row).Refusal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_caller_class_may_send_purge()
    {
        var row = ActionClassifier.Classify(_catalog, new PurgeIndexSourceRequest("github"));

        Assert.Null(Authorization.Decide(row.Class, A.Caller().AtTerminal().Client(ClientLabel.Cli).Build(), row).Refusal);
        Assert.Null(Authorization.Decide(row.Class, A.Caller().Human().Client(ClientLabel.Ui).Build(), row).Refusal);
        Assert.Null(Authorization.Decide(row.Class, A.Caller().Session().Launched().Build(), row).Refusal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_session_may_start_the_index_sync_service_like_a_human()
    {
        var row = ActionClassifier.Classify(_catalog, new RunServiceRequest(ServiceName.From("index-sync")));

        Assert.Null(Authorization.Decide(row.Class, A.Caller().Session().Build(), row).Refusal);
        Assert.Null(Authorization.Decide(row.Class, A.Caller().AtTerminal().Client(ClientLabel.Cli).Build(), row).Refusal);
    }
}
