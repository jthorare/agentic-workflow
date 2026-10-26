using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Features.Scoping;

internal static partial class ScopePacketChecks
{
    private static readonly Regex _regression = new(@"\bcontinue to\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static IEnumerable<JsonNode> Items(ScopeContext c) => ScopeJson.Items(c.Facts["acceptance"], "items");

    private static IEnumerable<JsonNode> Surfaces(ScopeContext c) => ScopeJson.Items(c.Facts["scopeMap"], "surfaces");

    /// <summary>SCP-ACC-001: a non-empty list whose items are failing and in EARS (or plain with a reason).</summary>
    private static void AcceptanceShape(ScopeContext c, RuleSink s)
    {
        if (!s.Need("acceptance"))
        {
            return;
        }

        var items = Items(c).ToList();
        if (items.Count == 0)
        {
            s.Fail(item: null, "the acceptance list is empty", "Write one acceptance item for each behaviour the change needs.");
        }

        foreach (var item in items)
        {
            ItemShape(item, s);
        }
    }

    private static void ItemShape(JsonNode item, RuleSink s)
    {
        var id = ScopeJson.Str(item, "id") ?? "?";
        if (string.IsNullOrEmpty(ScopeJson.Str(item, "id")))
        {
            s.Fail(id, "an acceptance item has no id", "Give every item an id AC-<n>.");
        }

        if (ScopeJson.Str(item, "status") is { } status && !string.Equals(status, "failing", StringComparison.Ordinal))
        {
            s.Fail(id, $"{id} has the status '{status}'; every item starts failing", "Only later stations hold evidence; leave status failing.");
        }

        var form = ScopeJson.Str(item, "form");
        if (!string.Equals(form, "ears", StringComparison.Ordinal) && !string.Equals(form, "plain", StringComparison.Ordinal))
        {
            s.Fail(id, $"{id} has the form '{form ?? "none"}'", "Use form ears, or plain with a reason.");
        }
        else if (string.Equals(form, "plain", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(ScopeJson.Str(item, "reason")))
        {
            s.Fail(id, $"{id} is plain without a reason", "Say what is missing: the trigger, the condition or the response.");
        }
    }

    /// <summary>SCP-ACC-002: every in-scope surface has an acceptance item, or is marked no-behaviour-change with a reason.</summary>
    private static void SurfacesHaveItems(ScopeContext c, RuleSink s)
    {
        if (!s.Need("scopeMap", "acceptance"))
        {
            return;
        }

        var covered = Items(c).SelectMany(static i => ScopeJson.Strings(i, "surfaces")).ToHashSet(StringComparer.Ordinal);
        foreach (var surface in Surfaces(c))
        {
            var id = ScopeJson.Str(surface, "id") ?? "?";
            if (!covered.Contains(id) && string.IsNullOrWhiteSpace(ScopeJson.Str(surface, "noBehaviourChange")))
            {
                s.Fail(id, $"surface {id} has no acceptance item and is not marked noBehaviourChange", "Write an acceptance item for it, or mark it noBehaviourChange with a reason.");
            }
        }
    }

    /// <summary>SCP-ACC-003: every answer in the log maps to an item, an open question or a map change.</summary>
    private static void AnswersRecorded(ScopeContext c, RuleSink s)
    {
        if (!s.Need("answerLog", "acceptance", "scopeMap"))
        {
            return;
        }

        var known = Items(c).Select(static i => ScopeJson.Str(i, "id")).Concat(Surfaces(c).Select(static x => ScopeJson.Str(x, "id")))
            .Concat(ScopeJson.Items(c.Facts["openQuestions"]).Select(static q => ScopeJson.Str(q, "id")))
            .Concat(ScopeJson.Items(c.Facts["questions"]).Select(static q => ScopeJson.Str(q, "id")))
            .OfType<string>().ToHashSet(StringComparer.Ordinal);
        var log = ScopeJson.Items(c.Facts["answerLog"]).ToList();
        var superseded = log.SelectMany(static e => ScopeJson.Strings(e, "supersedes")).ToHashSet(StringComparer.Ordinal);
        foreach (var entry in log.Where(e => !superseded.Contains(ScopeJson.Str(e, "id") ?? string.Empty)))
        {
            var id = ScopeJson.Str(entry, "id") ?? "?";
            var recorded = ScopeJson.Strings(entry, "recordedAs").ToList();
            if (recorded.Count == 0)
            {
                var questions = string.Join(", ", ScopeJson.Strings(entry, "questionIds"));
                s.Fail(id, questions.Length == 0 ? $"answer {id} maps to no acceptance item, open question or map change" : $"answer {id} (to {questions}) maps to no acceptance item, open question or map change", "Record the answer in an item, an open question or a map change (recordedAs).");
            }
            else if (recorded.FirstOrDefault(r => !known.Contains(r)) is { } unknown)
            {
                s.Fail(id, $"answer {id} is recorded as {unknown}, which does not exist", "Name an AC, S or Q id the facts hold.");
            }
        }
    }

    /// <summary>SCP-ACC-004: item and surface ids are unique.</summary>
    private static void IdsUnique(ScopeContext c, RuleSink s)
    {
        if (!s.Need("acceptance", "scopeMap"))
        {
            return;
        }

        foreach (var group in Items(c).Select(static i => ScopeJson.Str(i, "id")).OfType<string>().GroupBy(static i => i, StringComparer.Ordinal).Where(static g => g.Skip(1).Any()))
        {
            s.Fail(group.Key, $"acceptance id {group.Key} is used by more than one item", "An id is never reused for other text; give the new item the next id.");
        }

        foreach (var group in Surfaces(c).Select(static i => ScopeJson.Str(i, "id")).OfType<string>().GroupBy(static i => i, StringComparer.Ordinal).Where(static g => g.Skip(1).Any()))
        {
            s.Fail(group.Key, $"surface id {group.Key} is used by more than one surface", "An id is never reused; give the new surface the next id.");
        }
    }

    /// <summary>SCP-ACC-005: every related-feature surface has a regression item, or is marked no-behaviour-change.</summary>
    private static void RegressionItems(ScopeContext c, RuleSink s)
    {
        if (!s.Need("scopeMap", "acceptance"))
        {
            return;
        }

        var items = Items(c).ToList();
        foreach (var surface in Surfaces(c).Where(static x => string.Equals(ScopeJson.Str(x, "lens"), "related", StringComparison.Ordinal)))
        {
            var id = ScopeJson.Str(surface, "id") ?? "?";
            var regression = items.Exists(i => ScopeJson.Strings(i, "surfaces").Contains(id, StringComparer.Ordinal) && _regression.IsMatch(ScopeJson.Str(i, "text") ?? string.Empty));
            if (!regression && string.IsNullOrWhiteSpace(ScopeJson.Str(surface, "noBehaviourChange")))
            {
                s.Fail(id, $"related surface {id} has no regression item", "Add a WHILE ... THE ... SHALL continue to ... item for it, or mark it noBehaviourChange with a reason.");
            }
        }
    }
}
