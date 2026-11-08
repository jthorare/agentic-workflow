using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.CreateMemory;
using Aw.Sindri.Features.ForgetMemoryFact;
using Aw.Sindri.Features.WriteMemoryFact;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// Memory stores and facts (agent-first PR 13): create a store, write a fact, forget facts. Desk class like the item writes, but an agent session may run all three
/// (SessionOk), the way the MCP memory_* tools and 'sindri memory' do. The reads (list, search) are queries and have no row.
/// </summary>
internal static class MemoryRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("CreateMemory"), typeof(CreateMemoryRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/memories", ShellRows.Form("sindri memory create <name> [--description <text>]"), "ready", SessionOk: true),
        new(ActionId.From("WriteMemoryFact"), typeof(WriteMemoryFactRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/memories/{name}/facts", ShellRows.Form("sindri memory write <name> <text> [--tag <tag>] [--expires-at <time>]"), "ready", SessionOk: true),
        new(ActionId.From("ForgetMemoryFact"), typeof(ForgetMemoryFactRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: true, ReadsViews: false, Shortcut: null,
            "DELETE", "/sindri/memories/{name}/facts", ShellRows.Form("sindri memory forget <name> [--fact <id>] [--tag <tag>] [--expired]"), "ready", SessionOk: true),
    ];
}
