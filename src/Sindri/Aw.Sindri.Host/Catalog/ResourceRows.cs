using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.PurgeResources;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// Resources (agent-first PR 14): purge an owner's provisioned data. Desk class, destructive, and an agent session may run it (SessionOk), the way the MCP resources_purge tool and
/// 'sindri resources purge' do; the service refuses while the owner is active or has a run in flight. The read (list) is a query and has no row.
/// </summary>
internal static class ResourceRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("PurgeResources"), typeof(PurgeResourcesRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: true, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/resources/purge", ShellRows.Form("sindri resources purge <owner>"), "ready", SessionOk: true),
    ];
}
