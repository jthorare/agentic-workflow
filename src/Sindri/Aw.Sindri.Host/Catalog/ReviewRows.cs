using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.SendReviewFix;
using Aw.Sindri.Features.SetReviewLogin;

namespace Aw.Sindri.Host.Catalog;

/// <summary>The PR review page's writes (spec §6.1, §9). Desk class like the item writes; the service itself refuses a session, because the login is the owner's.</summary>
internal static class ReviewRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("SetReviewLogin"), typeof(SetReviewLoginRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/reviews/login", ShellRows.Form("sindri profile set <key> <value>"), "ready"),
        new(ActionId.From("SendReviewFix"), typeof(SendReviewFixRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/reviews/{owner}/{repo}/{number}/fix", ShellRows.Form("sindri review fix <pr> <text>"), "ready"),
    ];
}
