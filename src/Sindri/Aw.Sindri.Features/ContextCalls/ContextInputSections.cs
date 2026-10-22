using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aw.Context.Core.Code;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// What the three context Step inputs hand to a Step. A fill must be a function of the stored state and stay under the runner's caps (256 KiB a section
/// and a typed result, 1 MiB a plan, SND-WF-019 otherwise): a re-fill after a restart, an eviction or a pull has to give the same digest (SND-WF-018 else),
/// and the Step must not fail because a map is big. So the views are made stable first (no clock-relative value) and both parts are bounded here.
/// </summary>
internal static class ContextInputSections
{
    /// <summary>The most bytes of text, and of typed result, one input hands over; three inputs stay under the plan cap.</summary>
    internal const int Budget = 120 * 1024;

    /// <summary>
    /// The most bytes of a typed result kept: the Step input runner's own cap for one (StepInputRunner.SectionCap). A typed result is read by a code
    /// Step, not put in a prompt, so the text's budget doesn't bound it: a large map's lenses view is over 120 KiB as JSON, and dropping it left the
    /// gather with no lenses view.
    /// </summary>
    internal const int ResultBudget = 256 * 1024;

    /// <summary>The honest section for an input that could not read its lens.</summary>
    /// <param name="section">The section name.</param>
    /// <param name="what">What was asked ("context lenses").</param>
    /// <param name="why">The reason (a code and message, or what was missing).</param>
    /// <param name="notes">Notes about arguments that were ignored.</param>
    /// <returns>The section, with no typed result.</returns>
    internal static InjectionSections Unavailable(string section, string what, string why, IReadOnlyList<string> notes) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal) { [section] = Notes(notes) + "The " + what + " are unavailable for this Step (" + why + "); it runs without them.\n" });

    /// <summary>The text and the typed result of a view, each within the budget.</summary>
    /// <param name="section">The section name.</param>
    /// <param name="text">The readable text.</param>
    /// <param name="notes">Notes about arguments that were ignored.</param>
    /// <param name="view">The view, as the route answers it.</param>
    /// <param name="json">The JSON options with the view's type.</param>
    /// <typeparam name="T">The view type.</typeparam>
    /// <returns>The sections; the typed result is left out (and the text says so) when it is over the budget.</returns>
    internal static InjectionSections Filled<T>(string section, string text, IReadOnlyList<string> notes, T view, JsonSerializerOptions json)
    {
        var result = JsonSerializer.SerializeToElement(view, (JsonTypeInfo<T>)json.GetTypeInfo(typeof(T)));
        var left = Encoding.UTF8.GetByteCount(result.GetRawText()) > ResultBudget;
        var body = Notes(notes) + Bound(text) + (left ? "[the typed result is over 256 KiB and is left out]\n" : string.Empty);
        return new(new Dictionary<string, string>(StringComparer.Ordinal) { [section] = body }, left ? null : result);
    }

    /// <summary>The text cut at whole lines to fit the budget, with the number of lines left out.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text, or its head and a footer.</returns>
    internal static string Bound(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) <= Budget)
        {
            return text;
        }

        var lines = text.EndsWith('\n') ? text[..^1].Split('\n') : text.Split('\n');
        var kept = new StringBuilder();
        var used = 0;
        var shown = 0;
        foreach (var line in lines)
        {
            var size = Encoding.UTF8.GetByteCount(line) + 1;
            if (used + size > Budget - 256)
            {
                break;
            }

            _ = kept.Append(line).Append('\n');
            used += size;
            shown++;
        }

        return kept.Append(System.Globalization.CultureInfo.InvariantCulture, $"[{lines.Length - shown} more lines not shown: this section is capped at 120 KiB]\n").ToString();
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
