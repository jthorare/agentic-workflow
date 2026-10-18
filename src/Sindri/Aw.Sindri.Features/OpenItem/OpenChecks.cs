using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.OpenItem;

/// <summary>
/// What must hold before an open is recorded (UI spec E-1), in this order: the item exists and the caller may see it (404), it is still open and at the
/// version the human saw (SND-ITEM-409), no request for it is in flight (SND-LAUNCH-008), and an attach or resume has a session to address
/// (SND-LAUNCH-006). The launch in flight counts only while it is inside its 30 s deadline.
/// </summary>
/// <param name="items">Resolves the item by id or alias.</param>
/// <param name="launches">The 'launches' view.</param>
/// <param name="requests">The opens this process appended that the view hasn't shown yet, and the turn that serializes them.</param>
/// <param name="clock">The time, for the in-flight window.</param>
internal sealed class OpenChecks(IItemResolver items, ILaunchViews launches, OpenRequests requests, TimeProvider clock)
{
    /// <summary>Waits for this process's turn to check and record an open; dispose the result to give it up.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The turn.</returns>
    public ValueTask<IDisposable> Turn(CancellationToken cancellationToken) => requests.Turn(cancellationToken);

    /// <summary>Notes that a request was appended for the item, until the view shows it.</summary>
    /// <param name="item">The item.</param>
    /// <param name="launch">The request's launch.</param>
    public void Note(ItemId item, LaunchId launch) => requests.Note(item, launch);

    /// <summary>Checks <paramref name="request"/> for <paramref name="caller"/>.</summary>
    /// <param name="request">The open.</param>
    /// <param name="caller">The caller (a session sees only its repo's items).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The item and the session to address, or the refusal.</returns>
    public async ValueTask<Result<OpenAdmission>> Admit(OpenItemRequest request, CallerContext caller, CancellationToken cancellationToken)
    {
        var resolved = await items.Resolve(request.Item, caller, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var item = resolved.Value;
        if (Refusal(item, request.ItemVersion) is { } refused)
        {
            return refused;
        }

        var history = await launches.ForItem(item.Id, cancellationToken).ConfigureAwait(false);
        if (history.IsError)
        {
            return history.Error;
        }

        if (history.Value.FirstOrDefault(row => LaunchRules.InFlight(row, clock.GetUtcNow())) is { } inFlight)
        {
            return SindriErrors.LaunchInProgress.With($"{item.Id.Value} already has launch {inFlight.Id.Value} in progress (fix: wait for it to start or fail, about 30 s)");
        }

        if (requests.Unseen(item.Id, history.Value) is { } unseen)
        {
            return SindriErrors.LaunchInProgress.With($"{item.Id.Value} already has launch {unseen.Value} in progress (fix: wait for it to start or fail, about 30 s)");
        }

        var session = SessionFor(request, history.Value);
        return session.IsError ? session.Error : new OpenAdmission(item, session.Value);
    }

    private static Error? Refusal(ItemRow item, long seenVersion)
    {
        if (ItemStates.IsClosed(item.State))
        {
            return SindriErrors.ItemConflict.With($"{item.Id.Value} is {ItemStateNames.Of(item.State)}: a closed item can't be opened");
        }

        return item.Version != seenVersion
            ? SindriErrors.ItemConflict.With(string.Create(CultureInfo.InvariantCulture, $"{item.Id.Value} changed since you looked: it is at version {item.Version}, you saw {seenVersion}"))
            : null;
    }

    /// <summary>The launch whose session an attach or resume addresses (null for an open): the newest live one, or the newest with an id to resume. A picked agent narrows it to that agent's.</summary>
    private static Result<LaunchRow?> SessionFor(OpenItemRequest request, IReadOnlyList<LaunchRow> history)
    {
        if (request.Mode == LaunchMode.Open)
        {
            return (LaunchRow?)null;
        }

        var attach = request.Mode == LaunchMode.Attach;
        var ofTarget = history.Where(row => request.Target is not { } picked || row.Target == picked).ToList();
        var found = ofTarget.FirstOrDefault(row => attach ? row.Live : row.State == LaunchState.Started && !row.Live && row.ResumableId is not null);
        if (found is not null)
        {
            return found;
        }

        if (!attach && ofTarget.Exists(static row => row.Live))
        {
            return SindriErrors.LaunchNoSession.With("the item's session is still running, and a second agent on it would fight the first (fix: attach instead)");
        }

        return SindriErrors.LaunchNoSession.With(attach ? "the item has no live session (fix: open it instead)" : "the item has no session with an id to resume (fix: open it instead)");
    }
}
