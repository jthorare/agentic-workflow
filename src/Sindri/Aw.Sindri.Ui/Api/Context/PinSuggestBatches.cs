using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>
/// Sends a pasted list to GET /sindri/context/pins/suggest in calls whose encoded line= and def= parameters stay at <see cref="MaxQuery"/> characters (Kestrel refuses a request line over 8 KB, and a non-ASCII letter takes 6 or more),
/// and joins the answers, each batch's line indexes moved to where the batch sat in the list. A list of more than <see cref="MaxLines"/> lines is refused before any call, as the server refuses it.
/// </summary>
internal static class PinSuggestBatches
{
    /// <summary>The most lines the server takes in one list.</summary>
    internal const int MaxLines = 100;

    /// <summary>The most characters of line= and def= parameters in one call.</summary>
    internal const int MaxQuery = 6000;

    internal static async Task<Result<PinSuggestView>> Run(PinSuggestQuery query, Func<PinSuggestQuery, Task<Result<PinSuggestView>>> ask)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(ask);
        if (query.Lines is not { Count: > 0 } lines)
        {
            return await ask(query).ConfigureAwait(false);
        }

        if (lines.Count > MaxLines)
        {
            return new Error("SND-CONTEXT-179", ErrorKind.Invalid, string.Create(CultureInfo.InvariantCulture, $"line= has {lines.Count} lines; at most {MaxLines} (fix: send at most {MaxLines} lines)"));
        }

        var batches = Split(lines);
        if (batches.Count == 1)
        {
            return await ask(query).ConfigureAwait(false);
        }

        PinSuggestView? first = null;
        var matches = new List<PinSuggestMatch>(lines.Count);
        var offset = 0;
        foreach (var batch in batches)
        {
            var one = await ask(query with { Lines = batch }).ConfigureAwait(false);
            if (one.IsError)
            {
                return one.Error;
            }

            first ??= one.Value;
            var at = offset;
            matches.AddRange((one.Value.Matches ?? []).Select(m => m with { Line = m.Line + at }));
            offset += batch.Count;
        }

        return first is { } head ? head with { Matches = matches } : await ask(query).ConfigureAwait(false);   // the loop ran: more than one batch
    }

    // Rows go into a call while its parameters stay within the bound; a row alone always fits.
    private static List<List<PinSuggestLine>> Split(IReadOnlyList<PinSuggestLine> lines)
    {
        var batches = new List<List<PinSuggestLine>>();
        var size = 0;
        foreach (var line in lines)
        {
            var row = ContextRoutes.PinSuggestRowSize(line);
            if (batches.Count == 0 || size + row > MaxQuery)
            {
                batches.Add([]);
                size = 0;
            }

            batches[^1].Add(line);
            size += row;
        }

        return batches;
    }
}
