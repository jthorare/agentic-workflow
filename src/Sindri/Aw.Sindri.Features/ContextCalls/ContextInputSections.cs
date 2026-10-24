using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aw.Context.Core.Code;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// What the three context Step inputs hand to a Step. A fill must be a function of the stored state: a re-fill after a restart, an eviction or a pull has to give the same digest (SND-WF-018 else).
/// So the views are made stable first (no clock-relative value). Nothing is cut for size: a big map's view reaches the Step whole.
/// </summary>
internal static class ContextInputSections
{
    /// <summary>The honest section for an input that could not read its lens.</summary>
    /// <param name="section">The section name.</param>
    /// <param name="what">What was asked ("context lenses").</param>
    /// <param name="why">The reason (a code and message, or what was missing).</param>
    /// <param name="notes">Notes about arguments that were ignored.</param>
    /// <returns>The section, with no typed result.</returns>
    internal static InjectionSections Unavailable(string section, string what, string why, IReadOnlyList<string> notes) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal) { [section] = Notes(notes) + "The " + what + " are unavailable for this Step (" + why + "); it runs without them.\n" });

    /// <summary>The text and the typed result of a view, whole: no size cuts either (a code Step reads the typed result, and a prompt's size is the Step's concern).</summary>
    /// <param name="section">The section name.</param>
    /// <param name="text">The readable text.</param>
    /// <param name="notes">Notes about arguments that were ignored.</param>
    /// <param name="view">The view, as the route answers it.</param>
    /// <param name="json">The JSON options with the view's type.</param>
    /// <typeparam name="T">The view type.</typeparam>
    /// <returns>The sections and the typed result.</returns>
    internal static InjectionSections Filled<T>(string section, string text, IReadOnlyList<string> notes, T view, JsonSerializerOptions json)
    {
        var result = JsonSerializer.SerializeToElement(view, (JsonTypeInfo<T>)json.GetTypeInfo(typeof(T)));
        return new(new Dictionary<string, string>(StringComparer.Ordinal) { [section] = Notes(notes) + text }, result);
    }

    private static string Notes(IReadOnlyList<string> notes) => string.Concat(notes.Select(static n => "note: " + n + "\n"));

    /// <summary>The set view without its clock-relative values: 'asOf' is the map's creation, the map's age is unstated, and the sources' instants are left out.</summary>
    /// <param name="view">The view.</param>
    /// <returns>The same stored state, the same bytes at any clock.</returns>
    internal static LensesSetView Stable(LensesSetView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view with { AsOf = AsOf(view.Coverage), Coverage = Stable(view.Coverage), Searched = Stable(view.Searched) };
    }

    /// <summary>The history view without its clock-relative values.</summary>
    /// <param name="view">The view.</param>
    /// <returns>The same stored state, the same bytes at any clock.</returns>
    internal static HistoryView Stable(HistoryView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view with { AsOf = AsOf(view.Coverage), Coverage = Stable(view.Coverage), Searched = Stable(view.Searched) };
    }

    /// <summary>The code view without its clock-relative values.</summary>
    /// <param name="view">The view.</param>
    /// <returns>The same stored state, the same bytes at any clock.</returns>
    internal static CodeView Stable(CodeView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view with { AsOf = AsOf(view.Coverage), Coverage = Stable(view.Coverage), Searched = Stable(view.Searched) };
    }

    /// <summary>The query answer without its clock-relative values: a fixed query id, the Unix epoch for 'asOf' and each degraded source's 'since', and every citation unchecked.</summary>
    /// <param name="result">The answer.</param>
    /// <returns>The same stored state, the same bytes at any clock and for any query id.</returns>
    internal static ContextResult Stable(ContextResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result with
        {
            QueryId = QueryId.From("q_input"),
            AsOf = DateTimeOffset.UnixEpoch,
            Degraded = [.. result.Degraded.Select(static d => d with { Since = DateTimeOffset.UnixEpoch })],
            Cites = result.Cites.OrderBy(static c => c.Key, StringComparer.Ordinal).ToDictionary(static c => c.Key, static c => c.Value with { State = CitationState.Unchecked }, StringComparer.Ordinal),
        };
    }

    private static DateTimeOffset AsOf(LensCoverage coverage) => coverage.Map?.Created ?? DateTimeOffset.UnixEpoch;

    private static LensCoverage Stable(LensCoverage coverage) => coverage.Map is { } map ? coverage with { Map = map with { AgeDays = -1 } } : coverage;

    private static LensSearched Stable(LensSearched searched) => searched with { Sources = [.. searched.Sources.Select(static s => s with { AsOf = null })] };
}
