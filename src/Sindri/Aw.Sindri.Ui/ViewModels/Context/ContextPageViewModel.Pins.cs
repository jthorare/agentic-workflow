namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The concept layer's pin side on the page (<see cref="ContextPageViewModel"/>, continued): the pins read beside the concepts, kept current as events arrive.</summary>
internal sealed partial class ContextPageViewModel
{
    // The live pins against the approved profile and the map rebuild state. A 404 or any failure leaves the layer without pin buttons (an older server).
    private async Task ReadPins(Uri api, CancellationToken cancellationToken)
    {
        var read = await Ports.Api.ContextPins(api, _repo ?? string.Empty, cancellationToken).ConfigureAwait(true);
        if (read.IsSuccess)
        {
            Concepts.ShowPins(read.Value);
        }
        else
        {
            Concepts.PinsNotServed();
        }
    }

    // After every event: while the layer is up and the server serves pins, the cards follow the approval and the rebuild. A map newer than the concepts read is read again, so a waiting card is swapped.
    private async Task RefreshPins(Uri api, CancellationToken cancellationToken)
    {
        if (!Concepts.PinsServed || OpenedConceptId is not null || !Concepts.HasConcepts)
        {
            return;
        }

        var read = await Ports.Api.ContextPins(api, _repo ?? string.Empty, cancellationToken).ConfigureAwait(true);
        if (read.IsError)
        {
            return;
        }

        if (read.Value.MapVersion is { } version && !string.Equals(version, _conceptsVersion, StringComparison.Ordinal))
        {
            await ReloadConcepts(api, cancellationToken).ConfigureAwait(true);
        }

        Concepts.ShowPins(read.Value);
        ChangedConcepts();
    }

    // The concepts of the map that landed, without the layer's loading state: cards and the glossary change in place.
    private async Task ReloadConcepts(Uri api, CancellationToken cancellationToken)
    {
        var read = await ReadConcepts(api, cancellationToken).ConfigureAwait(true);
        if (read.IsError || !string.Equals(read.Value.State, "ok", StringComparison.Ordinal))
        {
            return;
        }

        Concepts.Show(read.Value);
        _conceptsVersion = read.Value.MapVersion;
        await ReadGlossary(api, read.Value.MapVersion, cancellationToken).ConfigureAwait(true);
    }

    // A pinned card's Try again: queues the refresh the Context page's own Refresh map queues, and reads the pins again.
    private async Task TryRefreshAgain()
    {
        if (Ports.ApiUrl() is not { } api)
        {
            return;
        }

        _ = await Ports.Api.RefreshContextMap(api, CancellationToken.None).ConfigureAwait(true);
        await ReadPins(api, CancellationToken.None).ConfigureAwait(true);
    }
}
