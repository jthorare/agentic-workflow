using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Pages;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetPages;

/// <summary>
/// The pure part of SetPages (pages spec §3.1): the ids the sets get, where each page lives, the concepts named by name resolved to ids, and the write rules over the whole page list as it will be after the write.
/// The request-only checks (<see cref="Precheck"/>) run inside the start's gate; the rest need the live profile and the map's facts and run at the start and again when the job runs.
/// </summary>
internal static class PagePlanner
{
    private const string ConceptPrefix = "concept:";

    /// <summary>The checks that need nothing but the request: something to do, no id twice, no page both set and removed.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Success, or SND-CONTEXT-256 / 254.</returns>
    internal static ValueTask<Result<Unit>> Precheck(SetPagesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Clashes(request));
    }

    /// <summary>Works out the request over the live profile and the map's facts.</summary>
    /// <param name="request">The request.</param>
    /// <param name="profile">The live profile.</param>
    /// <param name="map">What the current map says.</param>
    /// <returns>The plan, or the first refusal (SND-CONTEXT-250 to 257, or the profile's own).</returns>
    internal static Result<PagePlan> Plan(SetPagesRequest request, SindriProfile profile, PageMapFacts map)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(map);
        var clash = Clashes(request);
        if (clash.IsError)
        {
            return clash.Error;
        }

        var slots = Slots(profile);
        var pins = Pins(profile);
        var edits = new List<PageEdit>();
        var written = new List<PageWritten>();
        var applied = Apply(request, (slots, pins, profile), map, edits, written);
        if (applied is not null)
        {
            return applied;
        }

        var planned = PlannedProfile.Of(profile, slots);
        var rules = PageWriteCheck.Check(planned, slots, pins, map);
        return rules is not null ? rules : new PagePlan(edits, written, DiffOf(profile, planned));
    }

    // The sets, then the removals, in request order; the first refusal stops.
    private static Error? Apply(SetPagesRequest request, (List<PageSlot> Slots, PinNames Pins, SindriProfile Profile) live, PageMapFacts map, List<PageEdit> edits, List<PageWritten> written)
    {
        foreach (var set in request.Set ?? [])
        {
            var placed = Set(set, live.Slots, live.Profile, live.Pins, map);
            if (placed.IsError)
            {
                return placed.Error;
            }

            edits.Add(placed.Value.Edit);
            written.Add(placed.Value.Page);
        }

        foreach (var id in request.Remove ?? [])
        {
            var removed = Remove(id, live.Slots);
            if (removed.IsError)
            {
                return removed.Error;
            }

            edits.Add(new PageEdit(id, Entry: null, removed.Value.Repo));
            written.Add(removed.Value);
        }

        return null;
    }

    private static Result<Unit> Clashes(SetPagesRequest request)
    {
        if ((request.Set ?? []).Count == 0 && (request.Remove ?? []).Count == 0)
        {
            return ContextErrors.PageActionSpecInvalid.With("pages: nothing to set or remove (fix: give a page to set or remove)");
        }

        var named = new Dictionary<string, int>(StringComparer.Ordinal);
        return SetClash(request.Set ?? [], named) ?? RemoveClash(request.Remove ?? [], named) ?? (Result<Unit>)Unit.Value;
    }

    private static Error? SetClash(IReadOnlyList<PageSettings> sets, Dictionary<string, int> named)
    {
        for (var i = 0; i < sets.Count; i++)
        {
            var id = IdOf(sets[i]);
            if (named.TryGetValue(id, out var first))
            {
                return ContextErrors.PageIdDuplicate.With(Text($"index.pages[{i}].id: '{id}' is already index.pages[{first}]'s (fix: --id, or rename one)"));
            }

            named[id] = i;
        }

        return null;
    }

    private static Error? RemoveClash(IReadOnlyList<PagePinId> removes, Dictionary<string, int> named)
    {
        var removing = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in removes)
        {
            if (named.ContainsKey(id.Value))
            {
                return ContextErrors.PageIdDuplicate.With($"'{id.Value}' is set and removed in one request (fix: do one of them)");
            }

            if (!removing.Add(id.Value))
            {
                return ContextErrors.PageIdDuplicate.With($"'{id.Value}' is removed twice in one request (fix: name it once)");
            }
        }

        return null;
    }

    private static string IdOf(PageSettings set) => set.Id?.Value ?? PageSlugs.Of(set.Name);

    private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    // Every page in profile order: the top-level list, then each repo's own.
    private static List<PageSlot> Slots(SindriProfile profile) =>
    [
        .. (profile.Index?.Pages ?? []).Select(static p => new PageSlot(p, repo: null)),
        .. (profile.Repos ?? []).SelectMany(static r => (r.Index?.Pages ?? []).Select(p => new PageSlot(p, r.Name))),
    ];

    // The live profile's pins: a pin not built yet is still a concept the pages may name.
    private static PinNames Pins(SindriProfile profile)
    {
        var all = (profile.Index?.Concepts ?? []).Concat((profile.Repos ?? []).SelectMany(static r => r.Index?.Concepts ?? [])).ToList();
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pin in all.Where(static p => !string.IsNullOrEmpty(p.Name)))
        {
            _ = byName.TryAdd(pin.Name ?? string.Empty, PinIdOf(pin));
        }

        return new PinNames(new HashSet<string>(all.Select(PinIdOf), StringComparer.Ordinal), byName);
    }

    private static string PinIdOf(ConceptSettings pin) => pin.Id?.Value ?? PinSlug.Of(pin.Name ?? string.Empty);

    private static Result<(PageEdit Edit, PageWritten Page)> Set(PageSettings set, List<PageSlot> slots, SindriProfile profile, PinNames pins, PageMapFacts map)
    {
        var id = PagePinId.From(IdOf(set));
        var at = slots.Find(s => !s.Removed && string.Equals(s.Entry.Id?.Value, id.Value, StringComparison.Ordinal));
        var refused = Refusal(set, at, slots, [.. (profile.Repos ?? []).Select(static r => r.Name)]);
        if (refused is not null)
        {
            return refused;
        }

        var entry = set with { Id = id, Actions = Actions(set, pins, map) };
        if (at is null)
        {
            slots.Add(new PageSlot(entry, repo: null));   // new pages are top-level; the writer inserts after the last top-level entry
        }
        else
        {
            at.Entry = entry;
        }

        return (new PageEdit(id, entry, at?.Repo), new PageWritten(id, set.Name ?? string.Empty, at?.Repo, at is null ? "created" : "updated"));
    }

    // What stops a set before it is placed: an entry with no id that has this id, a repo the page can't be in.
    private static Error? Refusal(PageSettings set, PageSlot? at, List<PageSlot> slots, List<string> repos)
    {
        var id = IdOf(set);
        if (at is null && MadeFrom(slots, id) is var made and >= 0)
        {
            return ContextErrors.PageIdDuplicate.With(Text($"index.pages[{slots.Count}].id: '{id}' is already index.pages[{made}]'s, made from its name (fix: give index.pages[{made}] an id: in the profile)"));
        }

        var placed = at is null ? Placement(set, slots.Count(static s => !s.Removed), repos) : Existing(set, at, slots.IndexOf(at));
        return placed.IsError ? placed.Error : null;
    }

    // The entry with no 'id:' that the build gives this id (-1: none). The writer matches by id only, so a write can't reach it.
    private static int MadeFrom(List<PageSlot> slots, string id) => slots.FindIndex(s => !s.Removed && s.Entry.Id is null && string.Equals(s.Key, id, StringComparison.Ordinal));

    private static Result<Unit> Existing(PageSettings set, PageSlot at, int position)
    {
        if (at.Repo is { } repo && set.Repo is { } asked && !string.Equals(asked, repo, StringComparison.Ordinal))
        {
            return ContextErrors.PageRepoAmbiguous.With(Text($"repos.{repo}.index.pages: the page's repo is '{repo}', not '{asked}' (fix: leave --repo out, or remove the page and set it again; it is index.pages[{position}])"));
        }

        return Unit.Value;
    }

    // A new page goes top-level: the repo it is in is the one named, the surface's (PageRules refuses several), or the profile's only one.
    private static Result<Unit> Placement(PageSettings set, int position, List<string> repos)
    {
        if (set.Repo is { } named)
        {
            return repos.Contains(named, StringComparer.Ordinal) ? Unit.Value : ContextErrors.PageRepoAmbiguous.With($"no repo '{named}' (fix: {OneOf(repos)})");
        }

        return set.Surface is not null || repos.Count < 2
            ? Unit.Value
            : ContextErrors.PageRepoAmbiguous.With(Text($"index.pages[{position}]: say which repo this page is in, {string.Join(" or ", repos)} (fix: --repo)"));
    }

    private static string OneOf(List<string> repos) => repos.Count == 0 ? "add the repo to the profile first" : "one of " + string.Join(", ", repos);

    // Every action gets an id (its name's slug, made unique within the page) and its concepts as ids.
    private static List<PageActionSettings>? Actions(PageSettings set, PinNames pins, PageMapFacts map)
    {
        if (set.Actions is null)
        {
            return null;
        }

        var taken = new HashSet<string>(set.Actions.Select(static a => a.Id?.Value).OfType<string>(), StringComparer.Ordinal);
        return [.. set.Actions.Select(a => a with
        {
            Id = a.Id ?? PageActionPinId.From(PageSlugs.Unique(PageSlugs.Of(a.Name), taken)),
            Concepts = a.Concepts is null ? null : [.. a.Concepts.Select(c => Concept(c, pins, map))],
        })];
    }

    // An id (a pin's or the map's, with or without 'concept:') stays as written; otherwise an exact name ignoring case, the pins' before the map's, becomes its id; otherwise it stays as written for the rules to refuse.
    private static string Concept(string written, PinNames pins, PageMapFacts map)
    {
        var text = written.Trim();
        var bare = text.StartsWith(ConceptPrefix, StringComparison.Ordinal) ? text[ConceptPrefix.Length..] : text;
        if (pins.Ids.Contains(bare) || (ConceptId.TryFrom(bare, out var id) && map.Facts.ConceptIds.Contains(id)))
        {
            return text;
        }

        if (pins.ByName.TryGetValue(text, out var pin))
        {
            return pin;
        }

        return map.Concepts.FirstOrDefault(c => string.Equals(c.Name, text, StringComparison.OrdinalIgnoreCase)) is { } derived ? derived.Id.Value : text;
    }

    private static Result<PageWritten> Remove(PagePinId id, List<PageSlot> slots)
    {
        var at = slots.FindIndex(s => !s.Removed && string.Equals(s.Entry.Id?.Value, id.Value, StringComparison.Ordinal));
        if (at < 0)
        {
            if (MadeFrom(slots, id.Value) is var made and >= 0)
            {
                return ContextErrors.PageIdDuplicate.With(Text($"index.pages[{made}] has no id: and makes the id '{id.Value}' from its name (fix: give it an id: in the profile)"));
            }

            var near = PageNearest.Of(id.Value, slots.Where(static s => !s.Removed).Select(static s => s.Key));
            return ContextErrors.PageNotFound.With($"no page is named '{id.Value}'" + (near.Count > 0 ? $" (fix: try {string.Join(", ", near)})" : " (fix: list the pages with context page list)"));
        }

        slots[at].Removed = true;
        return new PageWritten(id, slots[at].Entry.Name ?? id.Value, slots[at].Repo, "removed");
    }

    // The page rows of the profile's own diff, with their values: 'key: before -> after' under 'index.pages.' and 'repos.<r>.index.pages.'.
    private static List<string> DiffOf(SindriProfile live, SindriProfile planned) =>
        [.. ProfileDiff.Changes(live, planned)
            .Where(static c => c.Key.StartsWith("index.pages.", StringComparison.Ordinal) || (c.Key.StartsWith("repos.", StringComparison.Ordinal) && c.Key.Contains(".index.pages.", StringComparison.Ordinal)))
            .Select(static c => $"{c.Key}: {c.Before ?? "(none)"} -> {c.After ?? "(none)"}")];
}
