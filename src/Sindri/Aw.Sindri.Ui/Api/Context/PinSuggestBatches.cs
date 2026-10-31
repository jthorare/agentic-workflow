using Aw.Kernel;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>Sends a pasted list to GET /sindri/context/pins/suggest 25 lines at a time (the server takes 100, a row of the form is 80 characters) and joins the answers, each batch's line indexes moved to where the batch sat in the list.</summary>
internal static class PinSuggestBatches
{
    /// <summary>The lines of one call.</summary>
    internal const int Size = 25;

    internal static async Task<Result<PinSuggestView>> Run(PinSuggestQuery query, Func<PinSuggestQuery, Task<Result<PinSuggestView>>> ask)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(ask);
        if (query.Lines is not { Count: > Size } lines)
        {
            return await ask(query).ConfigureAwait(false);
        }

        PinSuggestView? first = null;
        var matches = new List<PinSuggestMatch>(lines.Count);
        for (var offset = 0; offset < lines.Count; offset += Size)
        {
            var one = await ask(query with { Lines = [.. lines.Skip(offset).Take(Size)] }).ConfigureAwait(false);
            if (one.IsError)
            {
                return one.Error;
            }

            first ??= one.Value;
            var at = offset;
            matches.AddRange((one.Value.Matches ?? []).Select(m => m with { Line = m.Line + at }));
        }

        return first is { } head ? head with { Matches = matches } : await ask(query).ConfigureAwait(false);   // the loop ran: more than one batch
    }
}
