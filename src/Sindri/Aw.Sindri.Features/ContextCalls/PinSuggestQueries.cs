using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Checks a pin-suggest request (SND-CONTEXT-179) and makes the engine's query of it.</summary>
internal static class PinSuggestQueries
{
    private const int MaxLines = 100;
    private const int MaxRow = 80;

    internal static Result<PinSuggestQuery> Of(GetContextPinSuggestRequest request)
    {
        var lines = request.Line ?? [];
        var asked = new[] { request.Concept is not null, request.Search is not null, lines.Count > 0 }.Count(static a => a);
        if (asked != 1)
        {
            return Invalid(asked == 0
                ? "ask for one of concept=, search= or line= (fix: send exactly one of them)"
                : "concept=, search= and line= are three different questions (fix: send exactly one of them)");
        }

        return request.Concept is { } concept ? Concept(request, concept)
            : request.Search is { } search ? Search(request, search)
            : Lines(request, lines);
    }

    private static Result<PinSuggestQuery> Concept(GetContextPinSuggestRequest request, string concept) =>
        NodeKey.TryFrom(concept, out var key) && key.Kind == NodeKind.Concept
            ? new PinSuggestQuery(MapRead.Current, request.Repo, key, Search: null, PinSuggestBuilder.DefaultLimit, Lines: null)
            : Invalid($"concept= '{concept}' is not a concept key (fix: name one like concept:billing)");

    private static Result<PinSuggestQuery> Search(GetContextPinSuggestRequest request, string search) =>
        string.IsNullOrWhiteSpace(search)
            ? Invalid("search= is empty (fix: send the text to find)")
            : new PinSuggestQuery(MapRead.Current, request.Repo, Concept: null, search.Trim(), Math.Clamp(request.Limit ?? PinSuggestBuilder.DefaultLimit, 1, PinSuggestBuilder.MaxLimit), Lines: null);

    private static Result<PinSuggestQuery> Lines(GetContextPinSuggestRequest request, IReadOnlyList<string> names)
    {
        var defs = request.Def ?? [];
        if (names.Count > MaxLines)
        {
            return Invalid(Say($"line= has {names.Count} lines; at most {MaxLines} (fix: send at most {MaxLines} lines)"));
        }

        if (defs.Count != 0 && defs.Count != names.Count)
        {
            return Invalid(Say($"def= has {defs.Count} values for {names.Count} lines (fix: send none, or one per line, by index)"));
        }

        var lines = new List<PinSuggestLine>(names.Count);
        for (var i = 0; i < names.Count; i++)
        {
            var definition = defs.Count == 0 ? null : defs[i];
            if (Refusal(i, names[i], definition) is { } refused)
            {
                return Invalid(refused);
            }

            lines.Add(new PinSuggestLine(names[i].Trim(), string.IsNullOrWhiteSpace(definition) ? null : definition.Trim()));
        }

        return new PinSuggestQuery(MapRead.Current, request.Repo, Concept: null, Search: null, PinSuggestBuilder.DefaultLimit, lines);
    }

    // Why a line can't be matched, or null. The definition is a clipped fragment, so only its length counts.
    private static string? Refusal(int index, string name, string? definition) =>
        string.IsNullOrWhiteSpace(name) ? Say($"line {index + 1} has no name (fix: send a name on every line)")
        : name.Trim().Length + (definition?.Trim().Length ?? 0) > MaxRow ? Say($"line {index + 1} is over {MaxRow} characters with its definition (fix: clip the definition so name + definition stay at {MaxRow})")
        : null;

    private static string Say(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);

    private static Error Invalid(string message) => ContextErrors.PinSuggestInvalid.With(message);
}
