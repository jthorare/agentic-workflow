using System.Text.Json;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.OpenTargets;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>
/// The 'desk:' rows of the instances that wait on the owner at a human-verb wait, read at request time: the folded view has no recipe, so it cannot know a wait
/// state's exit is the owner's click (<see cref="OwnerStopReader"/> finds them; no recipe is named). Any read that fails leaves that one row off, never the list.
/// </summary>
/// <param name="stops">Finds the instances at a human-verb wait.</param>
/// <param name="views">The 'items' view, for each item's title and repo.</param>
/// <param name="contents">Reads the pull request's status fact.</param>
/// <param name="scrubber">Scrubs the rows' text.</param>
/// <param name="clock">The time the instance's own sentence is served at.</param>
internal sealed class NeedsYouOwnerStops(OwnerStopReader stops, IViewReader views, IFactContents contents, ITextScrubber scrubber, TimeProvider clock)
{
    private const string PrStatus = "fact:prStatus";
    private const string FallbackLabel = "waiting on you";

    /// <summary>The owner-stop rows <paramref name="caller"/> may see.</summary>
    /// <param name="caller">The caller; a session sees only its repo's and the repo-less ones.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The rows; empty when the instances can't be listed, and a row whose read fails is left off.</returns>
    public async ValueTask<IReadOnlyList<NeedsYouRow>> Read(CallerContext caller, CancellationToken cancellationToken)
    {
        var rows = new List<NeedsYouRow>();
        foreach (var candidate in await stops.Candidates(cancellationToken).ConfigureAwait(false))
        {
            if (await Row(candidate, caller, cancellationToken).ConfigureAwait(false) is { } row)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private async ValueTask<NeedsYouRow?> Row(OwnerStopCandidate candidate, CallerContext caller, CancellationToken cancellationToken)
    {
        try
        {
            var view = candidate.View;
            var item = await views.Get<ItemRow>(ViewNames.Items, view.ItemId!.Value.Value, cancellationToken).ConfigureAwait(false);   // why: Candidates keeps only instances with an item
            if (item.IsError || item.Value is not { } row || !ItemScope.VisibleOrRepoless(caller, row.Repo))
            {
                return null;
            }

            var wait = new OwnerStopInput(candidate.State.DisplayName?.ToLowerInvariant() ?? FallbackLabel, Reason(view), await Pr(view, cancellationToken).ConfigureAwait(false));
            return NeedsYouRows.ForOwnerStop(row, view.Address, wait, view.Since, scrubber);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return null;
        }
    }

    // The instance's own sentence for the wait; a view whose status can't be built falls back to the address.
    private string Reason(InstanceView view)
    {
        try
        {
            if (InstanceViews.Status(view, clock.GetUtcNow()).Next is { Length: > 0 } next)
            {
                return next;
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // fall through to the plain words
        }

        return $"waiting on you ({view.Address.Value})";
    }

    // The pull request 'fact:prStatus' names, or null when the instance holds none or it can't be read.
    private async ValueTask<NeedsYouOpenPr?> Pr(InstanceView view, CancellationToken cancellationToken)
    {
        if (view.FactShas?.GetValueOrDefault(PrStatus) is not { } sha)
        {
            return null;
        }

        try
        {
            var bytes = await contents.Read(new FactValue("fact:sha256:" + sha, sha, HeadSha: null), cancellationToken).ConfigureAwait(false);
            return bytes.IsError ? null : PullOf(bytes.Value);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>The link and number a 'fact:prStatus' body names; null when it names no usable pair.</summary>
    internal static NeedsYouOpenPr? PullOf(ReadOnlyMemory<byte> body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var url = root.TryGetProperty("url", out var link) && link.ValueKind == JsonValueKind.String ? link.GetString() : null;
        var number = root.TryGetProperty("pr", out var pr) && pr.TryGetInt32(out var n) ? n : 0;
        return string.IsNullOrEmpty(url) || number <= 0 ? null : new NeedsYouOpenPr(url, number);
    }
}
