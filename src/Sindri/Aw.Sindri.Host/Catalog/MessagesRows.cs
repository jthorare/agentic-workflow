using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ConnectMessages;
using Aw.Sindri.Features.MarkAllRead;
using Aw.Sindri.Features.MoveReadPointer;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The Messages Release 1 writes (messages spec §8.2, §8.4): connect (the owner's only, so no session) and the local read pointers ('e' in Unread, 'u',
/// Mark all read and its Undo; a session may not run them either, §8.4). Each row is 'pending:&lt;phase&gt;' with its command type bound (M1s stubs):
/// the route is not mapped until its phase flips the row to 'ready'.
/// </summary>
internal static class MessagesRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        Row("ConnectMessages", typeof(ConnectMessagesRequest), variant: null, new("/sindri/messages/connect", "sindri messages connect [--manifest]", "M1d")),
        Row("MoveReadPointer:read", typeof(MoveReadPointerRequest), "read", new("/sindri/messages/conversations/{id}/read", "sindri messages read <conversation> <ts>", "M1h")),
        Row("MoveReadPointer:unread", typeof(MoveReadPointerRequest), "unread", new("/sindri/messages/conversations/{id}/unread", "sindri messages unread <conversation> <ts>", "M1h")),
        Row("MarkAllRead:read", typeof(MarkAllReadRequest), "read", new("/sindri/messages/lists/unread/read", "sindri messages read --all", "M1h")),
        Row("MarkAllRead:unread", typeof(MarkAllReadRequest), "unread", new("/sindri/messages/lists/unread/unread", "sindri messages unread --all", "M1h")),
    ];

    private readonly record struct RowRoute(string Route, string Cli, string Phase);

    private static CommandRow Row(string action, Type request, string? variant, RowRoute http) =>
        new(ActionId.From(action), request, variant, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", http.Route, ShellRows.Form(http.Cli), "pending:" + http.Phase, SessionOk: false);
}
