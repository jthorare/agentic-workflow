using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Checks;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The pure step from the page's facts to its model (PR review page spec §6.2, §6.3): header, strip, panels in the settings' order, and the grouped diff.</summary>
public static partial class ReviewPageBuilder
{
    private const string MovingText = "The PR is moving; reload when it settles.";
    private static readonly HashSet<string> _resolvedStates = new(["resolved", "inThisPr", "inThisPrUnchecked"], StringComparer.Ordinal);
    private static readonly string[] _stripOrder = ["requiredChecks", "docMap", "risk", "ci"];

    /// <summary>Builds the page.</summary>
    /// <param name="facts">What was read on one head.</param>
    /// <returns>The page model.</returns>
    public static ReviewPageModel Build(ReviewPageFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var panels = new List<ReviewPagePanel>();
        Dictionary<string, string> parts = new(StringComparer.Ordinal);
        foreach (var id in facts.Settings.Resolved.Panels)
        {
            if (Panel(id, facts) is { } built)
            {
                panels.Add(built.Panel);
                if (built.Strip is { } part)
                {
                    parts[id] = part;
                }
            }
        }

        // why: the strip reads in the order of the spec's line (open checks, claim, risky files, failing CI), whatever order the panels are in
        var strip = _stripOrder.Where(parts.ContainsKey).Select(id => new ReviewPageStrip(id, parts[id])).ToList();

        var pr = facts.Pr;
        var yours = string.Equals(pr.Author, facts.Login, StringComparison.OrdinalIgnoreCase);
        var posts = yours || (facts.SelfLogin is { } self && string.Equals(pr.Author, self, StringComparison.OrdinalIgnoreCase));
        var header = new ReviewPagePr(
            facts.Repo, facts.Number, pr.Title, pr.Author, yours, pr.Draft, pr.State, new ReviewPageHead(pr.HeadSha, Short(pr.HeadSha), pr.HeadRef), pr.Base, pr.Url,
            [.. pr.IssueKeys.Select(static k => new ReviewPageIssue(k, "sindri://linear/" + k))], posts);
        var settings = new ReviewPageSettings(facts.Settings.Name, facts.Settings.From, facts.Settings.Choices.Count > 0 ? new ReviewPagePick(facts.Settings.Choices) : null);
        return new ReviewPageModel(facts.AsOf, header, settings, facts.Moving ? new ReviewPageMoving(MovingText) : null, Offline: null, strip, panels, ReviewPageDiffs.Build(facts));
    }

    private static (ReviewPagePanel Panel, string? Strip)? Panel(string id, ReviewPageFacts f) => id switch
    {
        "requiredChecks" => RequiredChecksPanel(f),
        "docMap" => DocMapPanel(f),
        "testPlan" => TestPlanPanel(f),
        "ci" => CiPanel(f),
        "evidence" => EvidencePanel(f),
        "risk" => RiskPanel(f),
        "conversation" => ConversationPanel(f),
        _ => null,
    };

    private static ReviewPagePanel Ready(string id, ReviewPanelData data) => new(id, "ready", Text: null, data);

    private static ReviewPagePanel Empty(string id, string text, ReviewPanelData data) => new(id, "empty", text, data);

    private static ReviewPagePanel Error(string id, string text, ReviewPanelData data) => new(id, "error", text, data);

    private static string Short(string sha) => sha.Length <= 7 ? sha : sha[..7];

    private static string Plural(int n, string one, string many) => string.Create(CultureInfo.InvariantCulture, $"{n} ") + (n == 1 ? one : many);

    // ---- requiredChecks

    private static (ReviewPagePanel, string?) RequiredChecksPanel(ReviewPageFacts f)
    {
        const string id = "requiredChecks";
        if (!f.Files.Ok)
        {
            return (Error(id, "Couldn't decide checks: github failed: " + f.Files.Error, new RequiredChecksData([])), "Couldn't decide checks");
        }

        var claims = f.Library.Claims;
        bool Resolves(string reference) => claims.Any(c => string.Equals(c.Ref, reference, StringComparison.Ordinal) && _resolvedStates.Contains(c.State));
        var results = RequiredChecks.Evaluate(new RequiredCheckInputs(
            f.Settings.Resolved.Checks, [.. f.Files.Value.Files.Select(static x => x.Path)], f.Pr.HeadSha, f.Runs.Ok ? f.Runs.Value : [], f.Pr.Body, f.Ticked,
            claims.Count > 0 ? [.. claims.Select(static c => c.Ref)] : null, Resolves));
        var open = RequiredChecks.OpenCount(results);
        var text = open == 0 ? "none open" : string.Create(CultureInfo.InvariantCulture, $"{open} open");
        var rows = new RequiredChecksData([.. results.Select(Check)]);
        return (results.Count == 0 ? Empty(id, "No required checks for this project", rows) : Ready(id, rows), text);
    }

    private static ReviewPageCheck Check(RequiredCheckResult r)
    {
        var status = r.Status switch
        {
            RequiredCheckStatus.Pass => "pass",
            RequiredCheckStatus.Fail => "fail",
            RequiredCheckStatus.Running => "running",
            RequiredCheckStatus.Skipped => "skipped",
            RequiredCheckStatus.Claimed => "claimed",
            RequiredCheckStatus.NeedsTick => "needsTick",
            RequiredCheckStatus.Ticked => "ticked",
            _ => "fail",
        };
        return new ReviewPageCheck(r.Key, r.Title, r.Need, status, r.Status == RequiredCheckStatus.Claimed ? "claimed in body" : r.Detail, r.Status == RequiredCheckStatus.Fail);
    }

    // ---- docMap and testPlan

    private static string HiddenText(int n) => $"{Plural(n, "doc", "docs")} not visible to this session";

    private static (ReviewPagePanel, string?) DocMapPanel(ReviewPageFacts f)
    {
        const string id = "docMap";
        var lib = f.Library;
        if (lib.Error is { } error)
        {
            return (Error(id, "Couldn't read the library: " + error, new DocMapData([], Doc: null, [], [])), null);
        }

        if (lib.Doc is null && lib.Claims.Count == 0)
        {
            return (Empty(id, lib.Hidden > 0 ? HiddenText(lib.Hidden) : "This PR names no design doc", new DocMapData([], Doc: null, [], lib.ProjectDocs)), "No doc named");
        }

        var items = lib.Claims.Select(static c => c.Ref[(c.Ref.IndexOf('#', StringComparison.Ordinal) + 1)..]).Distinct(StringComparer.Ordinal).ToList();
        var data = new DocMapData(lib.Claims, lib.Doc is null ? null : Tree(lib.Doc), [], []);
        return (Ready(id, data), "Covers " + string.Join(", ", items));
    }

    private static ReviewPageDoc Tree(ReviewPageDocFact doc) =>
        new(doc.Key, doc.Title, doc.Path, [.. doc.Items.Where(i => i.Parent is null || doc.Items.All(p => p.Key != i.Parent)).Select(i => Node(i, doc.Items))]);

    private static ReviewPageDocItem Node(ReviewPageItemFact item, IReadOnlyList<ReviewPageItemFact> all)
    {
        var unclaimed = item.Kind == "req" && !item.ClaimedHere && item.ClaimedBy.Count == 0;
        return new ReviewPageDocItem(item.Key, item.Title, item.Kind, item.ClaimedHere, item.ClaimedBy, unclaimed, unclaimed, [.. all.Where(c => c.Parent == item.Key).Select(c => Node(c, all))]);
    }

    private static (ReviewPagePanel, string?) TestPlanPanel(ReviewPageFacts f)
    {
        const string id = "testPlan";
        var lib = f.Library;
        return lib.Error is { } error ? (Error(id, "Couldn't read the library: " + error, new TestPlanData([])), null)
            : lib.TestRows.Count > 0 ? (Ready(id, new TestPlanData(lib.TestRows)), null)
            : (Empty(id, lib.Hidden > 0 && lib.Doc is null ? HiddenText(lib.Hidden) : "No test plan rows for the claimed items", new TestPlanData([])), null);
    }

    // ---- ci

    private static bool IsRunning(string conclusion) => conclusion.Length == 0 || string.Equals(conclusion, "pending", StringComparison.Ordinal);

    private static bool Failed(string conclusion) => conclusion is not ("success" or "neutral" or "skipped" or "pending" or "");

    private static (ReviewPagePanel, string?) CiPanel(ReviewPageFacts f)
    {
        const string id = "ci";
        if (!f.Runs.Ok)
        {
            return (Error(id, "Couldn't read CI: " + f.Runs.Error, new CiData([], Babysitter: null)), null);
        }

        var runs = f.Runs.Value;
        if (runs.Count == 0)
        {
            return (Empty(id, $"No CI runs on {Short(f.Pr.HeadSha)} yet", new CiData([], Babysitter: null)), null);
        }

        var views = runs.Select(r => new ReviewPageRun(r.Name, r.App ?? "status", IsRunning(r.Conclusion) ? "in_progress" : "completed", IsRunning(r.Conclusion) ? null : r.Conclusion, Short(r.Sha), f.Pr.Url + "/checks")).ToList();
        var failing = runs.Count(r => Failed(r.Conclusion));
        return (Ready(id, new CiData(views, Babysitter: null)), failing > 0 ? string.Create(CultureInfo.InvariantCulture, $"{failing} failing") : null);
    }

    // ---- evidence

    private static (ReviewPagePanel, string?) EvidencePanel(ReviewPageFacts f)
    {
        const string id = "evidence";
        if (!f.Evidence.Ok)
        {
            return (Error(id, "Couldn't read " + f.Evidence.Error, new EvidenceData([])), null);
        }

        List<ReviewPageEvidenceItem> items = [.. f.Evidence.Value];
        if (f.Pr.Body is { } body && EvidenceHeading.IsMatch(body))
        {
            items.Add(new ReviewPageEvidenceItem("prBody", "Proof in the PR body", "pr-body", f.Pr.Url, "prNumber", f.Pr.UpdatedAt));
        }

        return (items.Count == 0 ? Empty(id, $"No evidence found for {Short(f.Pr.HeadSha)}", new EvidenceData([])) : Ready(id, new EvidenceData(items)), null);
    }

    [GeneratedRegex(@"^#{1,6}[ 	]+Evidence", RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EvidenceHeading { get; }

    [GeneratedRegex(@"^#{1,6}[ 	]+Risks?[^
]*
+(?<first>[^
#][^
]*)", RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RisksSection { get; }

    // ---- risk

    private static (ReviewPagePanel, string?) RiskPanel(ReviewPageFacts f)
    {
        const string id = "risk";
        var hits = new List<ReviewPageRiskHit>();
        var paths = f.Files.Ok ? f.Files.Value.Files.Select(static x => x.Path) : [];
        foreach (var path in paths)
        {
            if (f.Settings.Resolved.Risky.FirstOrDefault(r => r.Globs.Any(g => ScopeGlobs.Allows([g], path))) is { } risky)
            {
                hits.Add(new ReviewPageRiskHit(path, risky.Why, "settings"));
            }
        }

        var files = hits.Count;
        if (f.Pr.Body is { } body && RisksSection.Match(body) is { Success: true } m)
        {
            hits.Add(new ReviewPageRiskHit("PR body", m.Groups["first"].Value.Trim(), "body"));
        }

        var strip = files > 0 ? Plural(files, "risky file", "risky files") : hits.Count > 0 ? "risks noted in the body" : null;
        return (hits.Count == 0 ? Empty(id, "No risky paths touched", new RiskData([])) : Ready(id, new RiskData(hits)), strip);
    }

    // ---- conversation

    private static (ReviewPagePanel, string?) ConversationPanel(ReviewPageFacts f)
    {
        const string id = "conversation";
        if (!f.Threads.Ok)
        {
            return (Error(id, "Couldn't read threads: " + f.Threads.Error, new ConversationData([])), null);
        }

        var threads = f.Threads.Value.Select(static t => new ReviewPageThread(t.Key, t.Path, t.Line, t.Resolved, t.Outdated, [new ReviewPageComment(t.Author, t.Body, t.CreatedAt)])).ToList();
        return (threads.Count == 0 ? Empty(id, "No threads yet", new ConversationData([])) : Ready(id, new ConversationData(threads)), null);
    }
}
