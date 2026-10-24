#pragma warning disable MA0003, MA0127 // fixture records are positional; their literals are named by the record
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Testing.Views;

/// <summary>The recipes the fixtures instantiate. The dev recipes and workflow.step mirror their files under recipes/; the others are fixture-only.</summary>
public static class FixtureRecipes
{
    private const int Hour = 60;
    private const int Day = 24 * Hour;

    private static ShapeState S(string id, int? order, string? branchOf = null, bool terminal = false, bool desk = false, bool child = false, bool wait = false,
        ShapeTimeout? timeout = null, int? tokens = null, OnParked? onParked = null, string? block = null, string? childRecipe = null, bool agent = false) =>
        new(id, order, branchOf, terminal, desk, child, wait, timeout, tokens, onParked, block, childRecipe, agent ? "step.agent" : null);

    private static ShapeTimeout T(int minutes, TimeoutAction then) => new(TimeSpan.FromMinutes(minutes), then);

    private static ShapeInput In(string key, string? label, FactType type, string kind, string? generate = null) => new(key, label, type, kind, generate);

    private static ShapeOutput Out(string key, string label, FactType type, string from) => new(key, label, type, from);

    /// <summary>workflow.step v4 (recipes/sindri/workflow.step.yaml): produce, checks, verify, done; the revisions loop returns to produce.</summary>
    public static RecipeShape Step { get; } = new("workflow.step", 4, "f039848fd714d2a46a4d3d216083bff1815821cd3e3b4f1bc6402bbb44bd54fa", RecipeUrgency.Work,
        [S("produce", 1, desk: true, timeout: T(4 * Hour, TimeoutAction.Park), tokens: 1_000_000, block: "step.produce"), S("checks", 2, desk: true, timeout: T(30, TimeoutAction.Retry), block: "step.checks"),
            S("verify", 3, desk: true, timeout: T(Hour, TimeoutAction.Park), block: "step.verify"), S("done", 4, terminal: true)],
        [new ShapeLoop("revisions", "checks", "produce", 3), new ShapeLoop("revisions", "verify", "produce", 3)],
        [In("fact:spec", "Spec", FactType.Text, "text")], []);

    /// <summary>workflow.dev-desk-round-trip v1: export (desk), import, done; the rounds loop returns from import to export.</summary>
    public static RecipeShape DevDesk { get; } = new("workflow.dev-desk-round-trip", 3, "22e3d173c3030036026aa70ae192f0bbe64b5135960ec38d7f05976443595357", RecipeUrgency.Work,
        [S("export", 1, desk: true, timeout: T(Hour, TimeoutAction.Park), block: "step.produce"), S("import", 2, desk: true, timeout: T(30, TimeoutAction.Retry), block: "step.checks"), S("done", 3, terminal: true)],
        [new ShapeLoop("rounds", "import", "export", 2)],
        [In("fact:request", "Request", FactType.Text, "text")], [Out("fact:output", "Exported file", FactType.File, "export")]);

    /// <summary>workflow.dev-triage-three-state v1: desk, answer, done.</summary>
    public static RecipeShape DevTriage { get; } = new("workflow.dev-triage-three-state", 3, "b77152c8613ea86d8a64fdf9e9a140c23b1c9352fc06fcebe5c1254ca5e2ad93", RecipeUrgency.Triage,
        [S("desk", 1, desk: true, timeout: T(Hour, TimeoutAction.Park), block: "step.produce"), S("answer", 2, wait: true, timeout: T(Day, TimeoutAction.Park)), S("done", 3, terminal: true)], [],
        [In("fact:scope", "Scope", FactType.Text, "text"), In("fact:item", "Item", FactType.Item, "item")],
        [Out("fact:output", "Draft", FactType.File, "desk"), Out("fact:answer", "Decision", FactType.Decision, "answer")], Name: "{fact:scope} - {start:date}");

    /// <summary>workflow.item v1 (recipes/sindri/workflow.item.yaml): the item's own instance; its running wait starts the workflow.step child and propagates a park.</summary>
    public static RecipeShape Item { get; } = new("workflow.item", 3, "fbd360859893f44e3d8c2bd83de7ee8edd760fb66f5541c99f5edb0e89406176", RecipeUrgency.Work,
        [S("triaged", 1, wait: true, timeout: T(7 * Day, TimeoutAction.Escalate)), S("startable", 2, wait: true, timeout: T(3 * Day, TimeoutAction.Escalate)),
            S("claimed", 3, wait: true, timeout: T(10, TimeoutAction.Retry)),
            S("running", 4, child: true, timeout: T(14 * Day, TimeoutAction.Park), onParked: OnParked.Propagate, childRecipe: "workflow.step"),
            S("done", 5, terminal: true), S("dropped", 6, terminal: true)],
        [], [In("fact:item", "Item", FactType.Item, "item")], []);

    /// <summary>workflow.incident-response (fixture-only): the remediate loop is recorded as its back edge, verify to mitigate.</summary>
    public static RecipeShape Incident { get; } = new("workflow.incident-response", 4, "5d90e1b2", RecipeUrgency.Incident,
        [S("assess", 1, timeout: T(30, TimeoutAction.Park), agent: true), S("mitigate", 2, timeout: T(4 * Hour, TimeoutAction.Escalate), agent: true), S("verify", 3, timeout: T(30, TimeoutAction.Park), agent: true),
            S("comms", 4, child: true, timeout: T(2 * Hour, TimeoutAction.Escalate), onParked: OnParked.Ignore, childRecipe: "workflow.step"), S("resolved", 5, terminal: true)],
        [new ShapeLoop("remediate", "verify", "mitigate", 3)],
        [In("fact:alert", "Alert", FactType.Payload, "webhook")],
        [Out("fact:severity", "Severity", FactType.Text, "assess"), Out("fact:history", "Alert history", FactType.Payload, "assess"), Out("fact:postmortem", "Postmortem draft", FactType.Text, "comms")]);

    /// <summary>workflow.triage-alert (fixture-only): the classify state is a desk.</summary>
    public static RecipeShape Triage { get; } = new("workflow.triage-alert", 2, "d41f2a07", RecipeUrgency.Triage,
        [S("ingest", 1, timeout: T(15, TimeoutAction.Park), agent: true), S("classify", 2, desk: true, timeout: T(Day, TimeoutAction.Park), tokens: 1_000_000), S("route", 3, timeout: T(Hour, TimeoutAction.Park), agent: true), S("done", 4, terminal: true)], [],
        [In("fact:alert", "Alert", FactType.Payload, "webhook")],
        [Out("fact:severity", "Severity", FactType.Text, "classify"), Out("fact:route", "Route", FactType.Decision, "route"), Out("fact:item", "Item", FactType.Item, "route")]);

    /// <summary>workflow.project (fixture-only): the scoping challenge loop, a design desk and a build state that waits on its children.</summary>
    public static RecipeShape Project { get; } = new("workflow.project", 12, "a8c21e7f", RecipeUrgency.Work,
        [S("intake", 1, timeout: T(Hour, TimeoutAction.Park), agent: true), S("scoping", 2, timeout: T(4 * Hour, TimeoutAction.Park), agent: true), S("design", 3, desk: true, timeout: T(Day, TimeoutAction.Park)),
            S("build", 4, child: true, timeout: T(Day, TimeoutAction.Escalate), tokens: 4_000_000, onParked: OnParked.Ignore, childRecipe: "workflow.step"),
            S("verify", 5, timeout: T(2 * Hour, TimeoutAction.Park), agent: true), S("done", 6, terminal: true)],
        [new ShapeLoop("challenge", "scoping", "scoping", 3)],
        [In("fact:project", "Linear project", FactType.Payload, "linear-project"), In("fact:prd", "PRD", FactType.File, "file")],
        [Out("fact:scope", "Scope map", FactType.ScopeMap, "scoping"), Out("fact:packet", "Packet", FactType.Packet, "design"), Out("fact:evidence", "Evidence", FactType.Evidence, "build")], BudgetTokens: 6_000_000);

    /// <summary>workflow.intake-linear (fixture-only): polls a team and emits items.</summary>
    public static RecipeShape Intake { get; } = new("workflow.intake-linear", 1, "0b7e44d3", RecipeUrgency.Work,
        [S("poll", 1, wait: true, timeout: T(30, TimeoutAction.Escalate)), S("emit", 2, timeout: T(Hour, TimeoutAction.Park), agent: true), S("done", 3, terminal: true)], [],
        [In("fact:team", "Linear team", FactType.Text, "text")], []);

    /// <summary>workflow.fork-demo (fixture-only): two initial paths, so the band is a transition list.</summary>
    public static RecipeShape Fork { get; } = new("workflow.fork-demo", 1, "1f08c6a9", RecipeUrgency.Work,
        [S("start", 1, timeout: T(Hour, TimeoutAction.Park), agent: true), S("left", 2, timeout: T(Hour, TimeoutAction.Park), agent: true), S("right", null, "left", timeout: T(Hour, TimeoutAction.Park), agent: true),
            S("merge", 3, timeout: T(Hour, TimeoutAction.Park), agent: true), S("done", 4, terminal: true)], [],
        [In("fact:request", "Request", FactType.Text, "text")], []);

    /// <summary>
    /// workflow.design v7 (recipes/sindri/workflow.design.yaml): the same states, orders, branches, loops, inputs and outputs as the file (the recipe golden is held to them by
    /// RecipeViewsTests), not in <see cref="All"/> because the fixture's blocks and 'next' texts are not the file's. The lens log's 'needsOwner' leaves every question of the round in
    /// fact:escalation (a list) and the ask desk collects fact:answers, one entry per question; fact:answers is no declared output of the shipped recipe, so the instance fixture lists it as a pending fake.
    /// </summary>
    public static RecipeShape Design { get; } = new("workflow.design", 7, "e975aac7", RecipeUrgency.Work,
        [S("intake", 1, timeout: T(Hour, TimeoutAction.Park)), S("draft", 2, timeout: T(3 * Hour, TimeoutAction.Park), tokens: 1_000_000), S("check", 3, timeout: T(Hour, TimeoutAction.Park)),
            S("round", 4, timeout: T(Hour, TimeoutAction.Park)), S("lens", 5, timeout: T(2 * Hour, TimeoutAction.Park), tokens: 1_000_000), S("lens-log", 6, timeout: T(Hour, TimeoutAction.Park)),
            S("evidence", 7, timeout: T(Hour, TimeoutAction.Park)),
            S("ask", null, "lens-log", desk: true, timeout: T(7 * Day, TimeoutAction.Park), block: "step.lens.ask"),
            S("revise", null, "lens-log", timeout: T(3 * Hour, TimeoutAction.Park), tokens: 1_000_000),
            S("lens-desk", null, "lens-log", desk: true, timeout: T(7 * Day, TimeoutAction.Park), block: "step.design.lens-desk"),
            S("apply", null, "intake", timeout: T(2 * Hour, TimeoutAction.Park), tokens: 1_000_000),
            S("designed", 8, terminal: true), S("dropped", 9, terminal: true)],
        [new ShapeLoop("rounds", "check", "draft", 5), new ShapeLoop("lens-runs", "lens-log", "lens", 40), new ShapeLoop("rounds", "evidence", "draft", 5)],
        [In("fact:scopePacket", "Scope packet (frozen)", FactType.Packet, "packet"), In("fact:designRevision", "Revision from stop 2", FactType.Payload, "payload"),
            In("fact:priorDesignPacket", "Previous design packet", FactType.Packet, "packet")],
        [Out("fact:designPacket", "Design packet (candidate)", FactType.Packet, "evidence"), Out("fact:design", "Design", FactType.Payload, "evidence"),
            Out("fact:lensRounds", "Lens rounds", FactType.Payload, "evidence"), Out("fact:escalation", "Questions for you", FactType.Payload, "lens-log")]);

    /// <summary>
    /// workflow.scoping v9 (recipes/sindri/workflow.scoping.yaml; fixture-only, not in <see cref="All"/>, so the file-equality checks skip it): the states and the full file hash, with the interview's three facts
    /// ('fact:questions' from 'questions', 'fact:answers' from the 'interview' desk, 'fact:answerLog' from 'record') declared beside the six outputs the file lists, so the facts list shows them.
    /// The recipe golden is the real recipe's view (WorkflowViewGoldenTests), so the hash here is the file's.
    /// </summary>
    public static RecipeShape Scoping { get; } = new("workflow.scoping", 9, "8ce8892e922b78d872158f1c1afc02b04a1805558c0902fa9df28acc475bb3a1", RecipeUrgency.Work,
        [S("intake", 1, desk: true, block: "step.scope.intake", timeout: T(Hour, TimeoutAction.Park)),
            S("read-brief", 2, desk: true, block: "step.scope.read-brief", timeout: T(Hour, TimeoutAction.Park)),
            S("gather", 3, desk: true, block: "step.scope.gather", timeout: T(30, TimeoutAction.Park)),
            S("draft", 4, desk: true, block: "step.scope.draft", timeout: T(2 * Hour, TimeoutAction.Park), tokens: 1_000_000),
            S("check", 5, desk: true, block: "step.scope.check", timeout: T(Hour, TimeoutAction.Park)),
            S("questions", 6, desk: true, block: "step.interview.questions", timeout: T(Hour, TimeoutAction.Park), tokens: 1_000_000),
            S("interview", 7, desk: true, block: "step.interview.answer", timeout: T(2 * Day, TimeoutAction.Park)),
            S("record", 8, desk: true, block: "step.interview.record", timeout: T(Hour, TimeoutAction.Park)),
            S("apply", 9, desk: true, block: "step.interview.apply", timeout: T(Hour, TimeoutAction.Park), tokens: 1_000_000),
            S("reanchor", 10, desk: true, block: "step.scope.reanchor", timeout: T(Hour, TimeoutAction.Park)),
            S("challenge", null, "questions", desk: true, block: "step.scope.challenge", timeout: T(2 * Hour, TimeoutAction.Park), tokens: 1_000_000),
            S("challenge-log", null, "questions", desk: true, block: "step.scope.challenge-log", timeout: T(Hour, TimeoutAction.Park)),
            S("size", null, "questions", desk: true, block: "step.judge.choose", timeout: T(2 * Day, TimeoutAction.Park)),
            S("propose", null, "questions", desk: true, block: "step.scope.propose", timeout: T(Hour, TimeoutAction.Park)),
            S("evidence", null, "questions", desk: true, block: "step.scope.evidence", timeout: T(Hour, TimeoutAction.Park)),
            S("stop", null, "questions", desk: true, block: "step.scope.review", timeout: T(7 * Day, TimeoutAction.Park)),
            S("freeze", null, "questions", desk: true, block: "step.scope.freeze", timeout: T(Hour, TimeoutAction.Park)),
            S("withdraw", null, "questions", desk: true, block: "step.scope.withdraw", timeout: T(Hour, TimeoutAction.Park)),
            S("size-desk", null, "questions", desk: true, block: "step.desk.choose", timeout: T(2 * Day, TimeoutAction.Park)),
            S("read-tracker", null, "intake", desk: true, block: "step.scope.read-tracker", timeout: T(Hour, TimeoutAction.Park)),
            S("idea", null, "intake", child: true, childRecipe: "workflow.idea-to-brief", onParked: OnParked.Propagate, timeout: T(7 * Day, TimeoutAction.Park)),
            S("read-change", null, "intake", desk: true, block: "step.scope.read-change", timeout: T(Hour, TimeoutAction.Park)),
            S("scoped", 11, terminal: true), S("dropped", 12, terminal: true)],
        [new ShapeLoop("interview", "challenge-log", "questions", 4), new ShapeLoop("rescope", "check", "gather", 3), new ShapeLoop("rounds", "check", "draft", 6)],
        [In("fact:scopeInput", "What to scope", FactType.Payload, "payload")],
        [Out("fact:scopeMap", "Scope map", FactType.ScopeMap, "evidence"), Out("fact:acceptance", "Acceptance list", FactType.Payload, "evidence"), Out("fact:coverage", "Lens coverage", FactType.Payload, "evidence"),
            Out("fact:decision", "Size estimate", FactType.Payload, "size"), Out("fact:openQuestions", "Open questions", FactType.Payload, "evidence"), Out("fact:scopePacket", "Scope packet", FactType.Payload, "freeze"),
            Out("fact:questions", "Questions", FactType.Payload, "questions"), Out("fact:answers", "Answers", FactType.Payload, "interview"), Out("fact:answerLog", "Answer log", FactType.Payload, "record")]);

    /// <summary>Every shape.</summary>
    public static IReadOnlyList<RecipeShape> All { get; } = [Step, Item, DevDesk, DevTriage, Incident, Triage, Project, Intake, Fork];
}
