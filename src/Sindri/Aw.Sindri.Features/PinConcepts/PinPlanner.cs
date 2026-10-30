using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Extract;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.PinConcepts;

/// <summary>
/// The pure part of PinConcepts (concept pins spec §3.6): the ids the sets get, the entries they merge into, the removals the folder rules allow, and the profile's own rules over the result. The request-only checks
/// (<see cref="Precheck"/>) run inside the start's gate; the rest need the live profile and run at the start and again when the job runs.
/// </summary>
internal static class PinPlanner
{
    private const int NearestShown = 5;

    /// <summary>The checks that need nothing but the request: something to do, no id twice, no pin both set and removed.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Success, or SND-CONTEXT-171 / 173.</returns>
    internal static ValueTask<Result<Unit>> Precheck(PinConceptsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Clashes(request));
    }

    /// <summary>Works out the request over the live profile.</summary>
    /// <param name="request">The request.</param>
    /// <param name="profile">The live profile.</param>
    /// <returns>The plan, or SND-CONTEXT-171 / 173 to 176.</returns>
    internal static Result<PinPlan> Plan(PinConceptsRequest request, SindriProfile profile)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);
        var clash = Clashes(request);
        if (clash.IsError)
        {
            return clash.Error;
        }

        var concepts = (profile.Index?.Concepts ?? []).ToList();
        var draft = new Draft();
        var features = Sets(request, concepts, draft);
        var removals = Removals(request, concepts, profile, draft);
        if (removals is not null)
        {
            return removals;
        }

        if (Rules(profile, concepts) is { } invalid)
        {
            return invalid;
        }

        return new PinPlan(draft.Edits, draft.Pins, draft.Dropped, features);
    }

    private static Result<Unit> Clashes(PinConceptsRequest request)
    {
        if ((request.Set ?? []).Count == 0 && (request.Remove ?? []).Count == 0)
        {
            return ContextErrors.PinInvalid.With("pins: nothing to pin or unpin (fix: give at least one pin to set or remove)");
        }

        var named = new Dictionary<string, string>(StringComparer.Ordinal);
        if ((SameId(request, named) ?? SetAndRemoved(request, named)) is { } error)
        {
            return error;
        }

        return Unit.Value;
    }

    private static Error? SameId(PinConceptsRequest request, Dictionary<string, string> named)
    {
        foreach (var set in request.Set ?? [])
        {
            var id = IdOf(set);
            if (named.TryGetValue(id, out var first))
            {
                return ContextErrors.PinIdClash.With($"'{first}' and '{set.Name}' make the same id {id} (fix: --id, or rename one)");
            }

            named[id] = set.Name;
        }

        return null;
    }

    private static Error? SetAndRemoved(PinConceptsRequest request, Dictionary<string, string> named)
    {
        foreach (var id in request.Remove ?? [])
        {
            if (named.TryGetValue(id.Value, out var name))
            {
                return ContextErrors.PinIdClash.With($"'{name}' is set and removed in one request: {id.Value} (fix: do one of them)");
            }
        }

        return null;
    }

    // Merges each set into the working list and returns the exact feature keys they name (a glob is not looked up in the map).
    private static List<string> Sets(PinConceptsRequest request, List<ConceptSettings> concepts, Draft draft)
    {
        var features = new List<string>();
        foreach (var set in request.Set ?? [])
        {
            var (entry, existed) = Merge(set, concepts);
            var id = entry.Id!.Value;   // why: Merge always sets the id
            draft.Edits.Add(new ConceptEdit(id, entry));
            draft.Pins.Add(new PinWritten(id, set.Name, existed ? "updated" : "created"));
            features.AddRange((set.Features ?? []).Where(static f => !f.Contains('*', StringComparison.Ordinal)));
        }

        return [.. features.Distinct(StringComparer.Ordinal)];
    }

    private static Error? Removals(PinConceptsRequest request, List<ConceptSettings> concepts, SindriProfile profile, Draft draft)
    {
        foreach (var id in request.Remove ?? [])
        {
            var removed = Remove(id, request.WithFolderRules, concepts, profile, draft.Dropped);
            if (removed.IsError)
            {
                return removed.Error;
            }

            draft.Edits.Add(new ConceptEdit(id, Entry: null));
            draft.Pins.Add(new PinWritten(id, removed.Value, "removed"));
        }

        return null;
    }

    private static string IdOf(PinSet set) => set.Id?.Value ?? PinSlug.Of(set.Name);

    private static (ConceptSettings Entry, bool Existed) Merge(PinSet set, List<ConceptSettings> concepts)
    {
        var id = ConceptPinId.From(IdOf(set));
        var at = concepts.FindIndex(c => Matches(c, id.Value));
        if (at < 0)
        {
            var created = new ConceptSettings(id, set.Name, Text(set.Summary), List(set.Features), Aliases: List(set.Aliases));
            concepts.Add(created);
            return (created, false);
        }

        var stored = concepts[at];
        var merged = stored with
        {
            Id = id,
            Name = set.Name,
            Summary = set.Summary is null ? stored.Summary : Text(set.Summary),
            Features = set.Features is null ? stored.Features : List(set.Features),
            Aliases = set.Aliases is null ? stored.Aliases : List(set.Aliases),
        };
        concepts[at] = merged;
        return (merged, true);
    }

    private static Result<string> Remove(ConceptPinId id, bool withFolderRules, List<ConceptSettings> concepts, SindriProfile profile, List<string> dropped)
    {
        var at = concepts.FindIndex(c => Matches(c, id.Value));
        if (at < 0)
        {
            return Missing(id, concepts, profile);
        }

        var pin = concepts[at];
        var rules = new (string Key, IReadOnlyList<string>? Values)[] { ("paths", pin.Paths), ("dependsOn", pin.DependsOn), ("owns", pin.Owns) }.Where(static r => r.Values is { Count: > 0 }).ToList();
        var count = rules.Sum(static r => r.Values!.Count);   // why: Where keeps only lists with entries
        if (count > 0 && !withFolderRules)
        {
            return ContextErrors.PinHasFolderRules.With(string.Create(CultureInfo.InvariantCulture,
                $"pin {id.Value} has {count} folder {(count == 1 ? "rule" : "rules")} set outside the app (fix: --with-folder-rules to drop them too)"));
        }

        dropped.AddRange(rules.Select(r => $"{id.Value}.{r.Key}"));
        concepts.RemoveAt(at);
        return pin.Name ?? id.Value;
    }

    private static Error Missing(ConceptPinId id, List<ConceptSettings> concepts, SindriProfile profile)
    {
        var own = (profile.Repos ?? []).FirstOrDefault(r => (r.Index?.Concepts ?? []).Any(c => Matches(c, id.Value)));
        if (own is not null)
        {
            return ContextErrors.PinInRepoProfile.With($"pin {id.Value} is set in repo {own.Name}'s own profile (fix: edit it there)");
        }

        var near = Nearest(id.Value, concepts.Select(IdOfEntry));
        return ContextErrors.PinNotInProfile.With($"no pin is named '{id.Value}' in the profile" + (near.Count > 0 ? $" (fix: try {string.Join(", ", near)})" : " (fix: list the pins with context pin list)"));
    }

    // An entry written by hand may have no 'id': the build gives it the slug of its name, so that is the id a write names it by.
    private static string IdOfEntry(ConceptSettings pin) => pin.Id?.Value ?? PinSlug.Of(pin.Name ?? string.Empty);

    private static bool Matches(ConceptSettings pin, string id) => string.Equals(IdOfEntry(pin), id, StringComparison.Ordinal);

    private static string? Text(string? text) => string.IsNullOrEmpty(text) ? null : text;

    private static IReadOnlyList<string>? List(IReadOnlyList<string>? list) => list is { Count: > 0 } ? list : null;

    // The profile's own rules over the profile as it will be written; a refusal under 'index' or a repo's 'index' is a pin problem and keeps the rule's key path.
    private static Error? Rules(SindriProfile profile, List<ConceptSettings> concepts)
    {
        var checkedProfile = ProfileRules.Check(profile with { Index = (profile.Index ?? new IndexSettings()) with { Concepts = concepts } });
        if (!checkedProfile.IsError)
        {
            return null;
        }

        var error = checkedProfile.Error;
        return string.Equals(error.Code, SindriErrors.ProfileInvalid.Code, StringComparison.Ordinal) && (error.Message.StartsWith("index.", StringComparison.Ordinal) || error.Message.StartsWith("repos.", StringComparison.Ordinal))
            ? ContextErrors.PinInvalid.With(error.Message)
            : error;
    }

    private static List<string> Nearest(string wanted, IEnumerable<string> known) =>
        [.. known.Distinct(StringComparer.Ordinal).OrderBy(k => Distance(wanted, k)).ThenBy(static k => k, StringComparer.Ordinal).Take(NearestShown)];

    private static int Distance(string a, string b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var diagonal = row[0];
            row[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var above = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                diagonal = above;
            }
        }

        return row[b.Length];
    }

    // The writes, pins and dropped rules a plan collects while the sets and removals are worked out.
    private sealed class Draft
    {
        internal List<ConceptEdit> Edits { get; } = [];

        internal List<PinWritten> Pins { get; } = [];

        internal List<string> Dropped { get; } = [];
    }
}
