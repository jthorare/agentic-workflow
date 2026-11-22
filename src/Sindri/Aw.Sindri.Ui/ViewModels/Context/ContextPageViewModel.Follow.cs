using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>Following the stored map (<see cref="ContextPageViewModel"/>, continued): newer versions are read again.</summary>
internal sealed partial class ContextPageViewModel
{
    // After every event: a map not read yet is read; one that is drawn is read again when the stored map moved to another version (an import, a promote); an open concept's map follows the stored map too.
    private Task FollowWhole(CancellationToken cancellationToken)
    {
        if (!_wholeWanted || Whole.IsLoading || (Concepts.IsLoading && OpenedConceptId is null))
        {
            return Task.CompletedTask;
        }

        if (Concepts.HasConcepts && OpenedConceptId is null)
        {
            return ReadConceptsIfNewer(cancellationToken);   // the cards are the page: a newer map version may have none, or other ones
        }

        if (Whole.IsFailed)
        {
            return Task.CompletedTask;
        }

        return Whole.IsReady ? ReadIfNewer(cancellationToken) : _conceptsRead ? ReadWhole(cancellationToken) : OpenFirst(cancellationToken);
    }

    // The cheap status says which version is current; the map is read again only when that is not the one drawn. A person's own answer (a query, an open feature) is left alone until they are back on the whole map.
    private async Task ReadIfNewer(CancellationToken cancellationToken)
    {
        if (!OnWholeAtRest || Whole.Answer is not { } drawn || Ports.ApiUrl() is not { } api)
        {
            return;
        }

        if (await NewerMapNumber(api, drawn.MapVersion, cancellationToken).ConfigureAwait(true) is not { } number)
        {
            return;
        }

        if (OpenedConceptId is null && !Concepts.HasConcepts)
        {
            await ReadConceptsOfNewer(cancellationToken).ConfigureAwait(true);
            return;
        }

        await ReadWhole(followed: number, cancellationToken).ConfigureAwait(true);
    }

    // The stored map's version number when it is not the one drawn (drawnVersion, like "v3") and not one already tried; else null.
    private async Task<int?> NewerMapNumber(Uri api, string? drawnVersion, CancellationToken cancellationToken)
    {
        var status = await Ports.Api.MapStatus(api, anchor: null, cancellationToken).ConfigureAwait(true);
        if (!status.IsSuccess)
        {
            return null;
        }

        var number = status.Value.Version.Number;
        if (number == _rereadNumber || string.Equals(drawnVersion, string.Create(CultureInfo.InvariantCulture, $"v{number}"), StringComparison.Ordinal))
        {
            return null;
        }

        _rereadNumber = number;   // a repo whose numbers differ from the answer's is read once per version, never in a loop
        return number;
    }

    // The concept cards are on the stage with nothing opened (#3261): a newer map version starts the page over like a first open, so cards that are gone give way to the whole map and the empty line.
    private async Task ReadConceptsIfNewer(CancellationToken cancellationToken)
    {
        if (HasStack || _conceptsVersion is null || Ports.ApiUrl() is not { } api)
        {
            return;
        }

        if (await NewerMapNumber(api, _conceptsVersion, cancellationToken).ConfigureAwait(true) is not null)
        {
            await OpenFirst(cancellationToken).ConfigureAwait(true);
        }
    }

    // True when the person is on the whole map with nothing of their own open: no stack, no zoom, no area.
    private bool OnWholeAtRest => !HasStack && !Whole.IsZoomed && !Whole.IsAreaOpen;

    // The map drawn had no concepts (#3261): a newer version may have them, so the page starts over like a first open, concepts first, and falls back to the whole map when there are still none.
    // When neither read answers, the map drawn before stays (the version is not tried again), not a failure row that only a newer version caused.
    private async Task ReadConceptsOfNewer(CancellationToken cancellationToken)
    {
        var before = Whole.Answer;
        Whole.LeaveConcept();   // clears the drawn map, so the concept layer is not shown beside it
        ChangedWhole();
        var reading = OpenFirst(cancellationToken);
        var generation = _openGeneration;   // OpenFirst bumps it before its first await; a repo switch while it reads bumps it again
        await reading.ConfigureAwait(true);
        if (generation == _openGeneration && before is not null && Whole.IsFailed && !Concepts.HasConcepts)
        {
            Whole.Show(before);
            Head = Whole.Head;
            ChangedWhole();
        }
    }
}
