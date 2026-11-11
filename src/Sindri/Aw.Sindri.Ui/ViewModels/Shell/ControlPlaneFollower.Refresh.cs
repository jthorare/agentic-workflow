using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The follower's re-read of the status and the services view (<see cref="ControlPlaneFollower"/>, continued).</summary>
internal sealed partial class ControlPlaneFollower
{
    // Re-reads the status and the services view. "Views behind" (SND-API-011, the server's answer to an X-Sindri-After
    // the views haven't reached: at once when stalled, after 2 s when behind) is not trouble, and the sequence is used
    // up by it (best effort, API §2.4): the status is read once more at once, without it, so the pill and banner show
    // what Sindri says (stalled, rebuilding). Should that be behind too, the last data stays and the next beat reads
    // again. A projection that is still
    // catching up (cursor behind the ledger head) or rebuilding is read again too, since no further event need come; a
    // stalled one won't move by itself, so it waits for an event. A failed status ends the follow. A services read that
    // fails for any reason but "no such route" (SND-UI-003, before P2) leaves the banner as Sindri last said it.
    private async Task<Refreshed> Refresh(Uri url, CancellationToken cancellationToken)
    {
        _again = false;
        var status = await api.ReadStatus(url, cancellationToken).ConfigureAwait(true);
        if (status.IsError)
        {
            if (IsBehind(status.Error))
            {
                _again = true;
                return Refreshed.Behind;
            }

            _ = health.Failed(status.Error, url);
            return Refreshed.Failed;
        }

        health.Show(url, status.Value);
        StartAtHead(status.Value);
        _again = status.Value.Views is { Stalled: false } views && (views.Rebuilding || views.Cursor < views.LedgerHead);
        if (health.Catalog.Controls.Count == 0 && await api.Commands(url, cancellationToken).ConfigureAwait(true) is { IsSuccess: true } catalog)
        {
            health.ShowCatalog(url, catalog.Value);   // the read on connect failed: try again, so Rebuild views shows once Sindri answers
        }

        var services = await api.Services(url, cancellationToken).ConfigureAwait(true);
        if (services.IsSuccess)
        {
            health.ShowViews(services.Value);
        }
        else if (IsBehind(services.Error))
        {
            _again = true;
        }
        else if (string.Equals(services.Error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal))
        {
            health.ShowViews(view: null);   // before P2 there's no such route: no banner
        }

        await health.RefreshViews(url, cancellationToken).ConfigureAwait(true);   // the queue and the open item (U2)
        return Refreshed.Shown;
    }

    // #2778: with nothing received yet (a fresh connect, a new epoch or another process), the stream opens at the ledger
    // head, not at 0; the head event itself is still delivered.
    private void StartAtHead(StatusView status)
    {
        if (_cursor == 0 && status.Views is { LedgerHead: > 0 } views)
        {
            _cursor = views.LedgerHead - 1;
        }
    }

    private enum Refreshed
    {
        /// <summary>The status failed: the follow ends.</summary>
        Failed,

        /// <summary>The views are behind and nothing was shown: the last data stays, the next beat reads again.</summary>
        Behind,

        /// <summary>A status was shown.</summary>
        Shown,
    }

    private static bool IsBehind(Error error) => SindriApiReads.IsViewsBehind(error);
}
