#pragma warning disable MA0003, MA0127 // fixture records are positional; their literals are named by the record
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstanceFact;

namespace Aw.Sindri.Testing.Views;

/// <summary>
/// The wire shapes of API spec section 3.8 (E-20, E-22, E-23) filled from the screens fixture (2026-10-12, now 12:26)
/// and the two dev recipes' planned state lists. The golden files are these values serialized; the UI lanes build
/// against them. Children are partial until engine P7 (no agent runtime rows, workstreams only on PRJ-sync). Every instance
/// has its own status: its address, recipe stamp, commands and cancel sentence come from its summary row, and the status text
/// is rendered from the status fields (<see cref="StatusText"/>), so none of them can name another instance or disagree.
/// </summary>
public static class WorkflowViewFixtures
{
    /// <summary>The fixture's now.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 26, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int month, int day, int hour, int minute) => new(2026, month, day, hour, minute, 0, TimeSpan.Zero);

    private static DateTimeOffset Today(int hour, int minute) => At(10, 12, hour, minute);

    /// <summary>A hash as every hash on the wire is written: 'sha256:' and 64 hex digits (the prefix padded with zeros).</summary>
    private static string Hash(string prefix) => "sha256:" + prefix.PadRight(64, '0');

    private static ActionId Act(string id) => ActionId.From(id);

    private static List<ActionId> Acts(params string[] ids) => [.. ids.Select(Act)];

    private static readonly RecipeShape _step = FixtureRecipes.Step;
    private static readonly RecipeShape _item = FixtureRecipes.Item;
    private static readonly RecipeShape _incident = FixtureRecipes.Incident;
    private static readonly RecipeShape _triage = FixtureRecipes.Triage;
    private static readonly RecipeShape _project = FixtureRecipes.Project;
    private static readonly RecipeShape _intake = FixtureRecipes.Intake;
    private static readonly RecipeShape _devTriage = FixtureRecipes.DevTriage;
    private static readonly RecipeShape _devDesk = FixtureRecipes.DevDesk;
    private static readonly RecipeShape _fork = FixtureRecipes.Fork;

    private static Func<StateCell, StateCell> Shape(params (string State, Func<StateCell, StateCell> Change)[] changes) =>
        cell => changes.Where(c => c.State == cell.State).Aggregate(cell, (acc, c) => c.Change(acc));

    private static SubjectRef Subject(string kind, string key, string label, string? href = null) => new(kind, key, label, href);

    // ---- the instance list (E-20) ------------------------------------------------------------------------------------

    private static InstanceSummary Row(string address, RecipeShape shape, string state, DateTimeOffset since,
        EngineOwnedBadge? owned, SubjectRef subject, IReadOnlyList<StateCell> cells, string now, params string[] actions)
    {
        var states = cells.Select(c => owned is not null && c.State == state && c.Badge is null ? c with { Badge = owned } : c).ToList();
        return new(InstanceAddress.From(address), WorkflowInstanceId.Of(InstanceAddress.From(address)), ItemKey.From(address[..address.IndexOf('/', StringComparison.Ordinal)]), shape.Stamp, state,
            StateChip.For(owned, states.Exists(c => c.Desk && c.Glyph == StateGlyph.Active), states.Exists(c => c.Terminal && c.Glyph == StateGlyph.Done && c.State == state) ? OutcomeName.From("resolved") : null),
            since, owned, subject, shape.InputRows(), states, now.Length > 80 ? now[..79] + "…" : now, null, Acts(actions));
    }

    private static InstanceSummary DoneTriage(string address, string label, DateTimeOffset since) =>
        Row(address, _triage, "done", since, null, Subject("webhook", label[(label.IndexOf('#', StringComparison.Ordinal) + 1)..], label),
            _triage.Cells("done"), "done");

    private static readonly InstanceSummary _inc14 = Row("ITM-01JK1/incident-response/1", _incident, "mitigate", Today(11, 2), null,
        Subject("webhook", "P-2211", "PagerDuty P-2211 · Outbox retry storm"),
        _incident.Cells("mitigate", round: 0, Shape(
            ("mitigate", c => c with { Round = new Round(1, 3) }),
            ("assess", c => c with { Outcome = "sev2" }))),
        "agent rolling back in ITM-01JG2", "ApplyCommand:takeover", "ApplyCommand:cancel");

    private static readonly InstanceSummary _sentry8812 = Row("ITM-01JK2/triage-alert/1", _triage, "classify", Today(12, 22), null,
        Subject("webhook", "8812", "sentry #8812 TypeError in tabs.layout"), _triage.Cells("classify"),
        "needs your severity (desk answer)", "CompleteStep", "ApplyCommand:takeover", "ApplyCommand:cancel");

    private static readonly InstanceSummary _p2211 = Row("ITM-01JK6/triage-alert/1", _triage, "route", Today(11, 2),
        new EngineOwnedBadge(EngineOwnedKind.Escalated, "gate.severity 0.62, below 0.8"),
        Subject("webhook", "P-2211", "PagerDuty P-2211"), _triage.Cells("route"),
        "needs your answer", "ApplyCommand:answer-accept", "ApplyCommand:answer-reject", "ApplyCommand:takeover", "ApplyCommand:cancel") with
    { Answer = Answer(AnswerKind.Accept, "ITM-01JK6/triage-alert/1"), };

    private static readonly InstanceSummary _sentry8809 = Row("ITM-01JK7/triage-alert/1", _triage, "done", Today(11, 58), null,
        Subject("webhook", "8809", "sentry #8809 429s on stream"), _triage.Cells("done"), "done");

    private static readonly InstanceSummary _prjSync = Row("ITM-01JK3/project/1", _project, "build", Today(9, 14), null,
        Subject("linear-project", "APP", "APP · Offline sync", "https://linear.app/example/project/offline-sync"),
        _project.Cells("build", change: Shape(("build", c => c with { Children = new Progress(4, 9) }))),
        "engine waiting in ITM-01JK3", "ApplyCommand:takeover", "ApplyCommand:cancel");

    private static readonly InstanceSummary _prjExports = Row("ITM-01JK4/project/1", _project, "design", At(10, 11, 16, 2),
        new EngineOwnedBadge(EngineOwnedKind.TakenOver, "with you since 10-11 16:02"),
        Subject("linear-project", "ENG", "ENG · Billing exports"), _project.Cells("design"),
        "with you: import the design output", "ApplyCommand:handback", "ApplyCommand:cancel");

    // ITM-01JK5 of the screens fixture: PRD-tabs.md is given (the engine starts an instance with its required inputs).
    private static readonly InstanceSummary _prjTabs = Row("ITM-01JK5/project/1", _project, "scoping", Today(11, 40), null,
        Subject("linear-project", "ENG", "ENG · Tab manager v2"),
        _project.Cells("scoping", round: 1, Shape(("scoping", c => c with { Round = new Round(2, 3) }))),
        "agent challenging in ITM-01JK5", "ApplyCommand:takeover", "ApplyCommand:cancel");

    // Instances only the status layouts name: none of them is a row of the root list, so the layouts don't contradict it.
    private static readonly InstanceSummary _itemParked = Row("ITM-01JC8/item/1", _item, "running", Today(11, 54), new EngineOwnedBadge(EngineOwnedKind.Parked, "no-progress (child)"),
        Subject("item", "ITM-01JC8", "Retry storm in outbox dispatcher"),
        _item.Cells("running", change: Shape(("running", c => c with { Children = new Progress(0, 1) }))),
        "parked child", "ApplyCommand:answer-grant", "ApplyCommand:answer-text", "ApplyCommand:takeover", "ApplyCommand:cancel");

    // The same instance mirroring a child that decides the answer: it takes only takeover and cancel (no answer entry is served).
    private static readonly InstanceSummary _itemChildPointer = _itemParked with { Address = InstanceAddress.From("ITM-01JC9/item/1"), Actions = Acts("ApplyCommand:takeover", "ApplyCommand:cancel") };

    private static readonly InstanceSummary _budgetStep = Row("ITM-01JH5/step/1", _step, "produce", Today(11, 54), new EngineOwnedBadge(EngineOwnedKind.Parked, "budget"),
        Subject("text", "ITM-01JH5", "ITM-01JH5"), _step.Cells("produce"), "parked: budget", "ApplyCommand:answer-grant-tokens", "ApplyCommand:takeover", "ApplyCommand:cancel");

    // ITM-01JA1 of the screens fixture: taken over at 12:01, at its produce step. Layout 5: the covered state with the badge.
    private static readonly InstanceSummary _takenOver = Row("ITM-01JA1/step/1", _step, "produce", Today(12, 1),
        new EngineOwnedBadge(EngineOwnedKind.TakenOver, "with you since 12:01"),
        Subject("text", "ITM-01JA1", "ITM-01JA1"), _step.Cells("produce"), "with you", "ApplyCommand:handback", "ApplyCommand:cancel");

    // ITM-01JM3: its produce Step runs on an agent session (P8): not a row of the root list, and no desk task, so no complete command.
    private static readonly InstanceSummary _agentRun = Row("ITM-01JM3/step/1", _step, "produce", Today(12, 10), null,
        Subject("text", "ITM-01JM3", "ITM-01JM3"), _step.Cells("produce"), "agent running", "ApplyCommand:takeover", "ApplyCommand:cancel");

    // A needs-answer park of its own (PRJ-exports is the taken-over project): a project parked at its design desk. Not a row of the root list.
    private static readonly InstanceSummary _prjReports = Row("ITM-01JKE/project/1", _project, "design", Today(10, 30),
        new EngineOwnedBadge(EngineOwnedKind.Parked, "needs-answer"), Subject("linear-project", "ENG", "ENG · Reports"),
        _project.Cells("design"), "needs your answer",
        "ApplyCommand:answer-text", "ApplyCommand:takeover", "ApplyCommand:cancel");

    // A PREVIEW (MissingInputPreviews): the intake agent parked asking for the PRD, so the PRD input is missing. v1 never starts an instance with a missing
    // input; the golden workflow-instances/preview-missing-input gives the UI data for the missing and blocked renders (not a root row, not in the screens fixture).
    private static readonly InstanceSummary _prjNoPrd = Row("ITM-01JKH/project/1", _project, "intake", Today(12, 5), new EngineOwnedBadge(EngineOwnedKind.Parked, "needs-answer"),
        Subject("linear-project", "ENG", "ENG · Docs site"), _project.Cells("intake"), "needs your answer",
        "ApplyCommand:answer-text", "ApplyCommand:takeover", "ApplyCommand:cancel") with
    { Inputs = _project.InputRows("fact:prd"), };

    // A wait park of its own: dev-triage-three-state at its 'answer' wait, the reply couldn't be read. Not a root row.
    private static readonly InstanceSummary _triageAnswer = Row("ITM-01JL4/dev-triage-three-state/1", _devTriage, "answer", Today(12, 3), new EngineOwnedBadge(EngineOwnedKind.Parked, "needs-answer"),
        Subject("text", "Crash on launch", "Crash on launch - 2026-10-12"), _devTriage.Cells("answer"), "needs your answer",
        "ApplyCommand:answer-resume", "ApplyCommand:takeover", "ApplyCommand:cancel");

    private static readonly InstanceSummary _prjCache = Row("ITM-01JKF/project/1", _project, "scoping", Today(11, 50), new EngineOwnedBadge(EngineOwnedKind.Parked, "spec-conflict (drift)"),
        Subject("linear-project", "ENG", "ENG · Cache layer"), _project.Cells("scoping"), "parked: spec-conflict", "ApplyCommand:takeover", "ApplyCommand:cancel", "ApproveRecipe");

    private static string LinAddress(string team) => team == "ENG" ? "ITM-01JKB/intake-linear/1" : "ITM-01JKC/intake-linear/1";

    private static InstanceSummary Intake(string team, DateTimeOffset since, string now) =>
        Row(LinAddress(team), _intake, "poll", since, null, Subject("text", $"Linear {team}", $"Linear {team}"), _intake.Cells("poll"), now, "ApplyCommand:cancel");

    private static IReadOnlyList<InstanceSummary> Roots() =>
    [
        _inc14, _sentry8812, _prjSync, _prjExports, _prjTabs, _p2211, _sentry8809,
        DoneTriage("ITM-01JK8/triage-alert/1", "sentry #8801", Today(10, 30)),
        DoneTriage("ITM-01JK9/triage-alert/1", "sentry #8797", Today(10, 5)),
        DoneTriage("ITM-01JKA/triage-alert/1", "sentry #8790", Today(9, 40)),
        Intake("ENG", Today(12, 10), "engine waiting in ITM-01JKB"),
        Intake("APP", Today(12, 15), "engine waiting in ITM-01JKC"),
    ];

    private static ListCounts CountsOf(IReadOnlyList<InstanceSummary> roots) => new(
        roots.Count(r => r.Chip.Word != "DONE"), roots.Count(r => r.Chip.Tone == ChipTone.Attn),
        roots.Count(r => r.Chip.Word == "WITH YOU"), roots.Count(r => r.Chip.Word == "RUNNING"), roots.Count(r => r.Chip.Word == "DONE"));

    // The counts are over the whole root set, before the recipe filter or 'open' narrows the rows (API spec section 3.8).
    private static InstanceList Ordered(IEnumerable<InstanceSummary> rows) =>
        new([.. rows.OrderBy(r => r.Chip.Word == "DONE").ThenBy(IsStuck).ThenBy(r => r.Recipe.Urgency).ThenBy(r => r.Subject.Label, StringComparer.Ordinal)], CountsOf(Roots()), null, Now);

    // Parked and escalated instances sort behind the ones still moving: Needs you carries them, so the Home strip doesn't repeat them.
    private static bool IsStuck(InstanceSummary row) => row.EngineOwned?.Kind is EngineOwnedKind.Parked or EngineOwnedKind.Escalated;

    /// <summary>Every summary row the fixtures build: the 12 roots, and the instances only the status layouts and instance views name.</summary>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<InstanceSummary> AllSummaries() => [.. Roots(), _itemParked, _budgetStep, _takenOver, _prjReports, _prjCache, _prjDocs, _prjNoPrd, _triageAnswer];

    /// <summary>
    /// The addresses whose child group previews schema v2 fan-out (WF 3.9): several live children of one wait. A v1 child wait starts
    /// one child workflow, so every other instance has at most one live child; these goldens are re-recorded when v2 lands.
    /// </summary>
    public static IReadOnlySet<string> FanOutPreviews { get; } = new HashSet<string>(StringComparer.Ordinal) { "ITM-01JK3/project/1", "ITM-01JKG/project/1" };

    /// <summary>
    /// The addresses whose inputs preview a missing input. v1 serves every input of a started instance as required and present
    /// (StartInstance refuses a missing one; an optional input is a future recipe-schema change), so this row is a marked preview.
    /// </summary>
    public static IReadOnlySet<string> MissingInputPreviews { get; } = new HashSet<string>(StringComparer.Ordinal) { "ITM-01JKH/project/1" };

    /// <summary>The Workflows page preview of a missing required input (the INPUTS column's missing state).</summary>
    /// <returns>The list.</returns>
    public static InstanceList PreviewMissingInput() => Ordered([_prjNoPrd]);

    /// <summary>The Workflows page: the 12 roots, unfinished first, the parked and escalated behind the moving ones, then by urgency and subject label (API spec section 3.8).</summary>
    public static InstanceList RootList() => Ordered(Roots());

    /// <summary>The roots of recipe workflow.triage-alert.</summary>
    public static InstanceList RecipeFilter() => Ordered(Roots().Where(r => r.Recipe.Id.Value == "workflow.triage-alert"));

    /// <summary>The open roots ('?root=true&amp;open=true'): the rows not finished, counts unchanged.</summary>
    public static InstanceList OpenList() => Ordered(Roots().Where(r => r.Chip.Word != "DONE"));

    /// <summary>No instances.</summary>
    public static InstanceList EmptyList() => new([], new ListCounts(0, 0, 0, 0, 0), InstanceList.EmptyText, Now);

    // ---- the status fields (E-23, WF section 6.4) --------------------------------------------------------------------

    /// <summary>
    /// The status of one instance from its summary row: the header, the commands (the row's actions that are verbs, in the
    /// catalog's order) and the text, all from the row, so a status cannot name another instance.
    /// </summary>
    private static StatusFields Status(InstanceSummary row, string? detail, string? next, OpenWork open, StatusLines? lines = null)
    {
        lines ??= new StatusLines();
        var recipe = row.Recipe;
        // The header's v<n> is the instance's version (its mirrored event count). A fixture has no events, so it shows the recipe's author version as a stand-in: the goldens pin it.
        var header = new StatusHeader(row.Address, recipe.Id, recipe.Hash.Display, recipe.Version, HeaderState(row), detail, row.Since, (long)(Now - row.Since).TotalSeconds);
        var fields = new StatusFields(header, next, lines.Answer, lines.Child, lines.Drift, lines.Inputs, lines.Timeout, lines.Budget, lines.Paused,
            Commands(row, lines.Answer, open), string.Empty)
        {
            Run = lines.Run,
            TakenOverAt = lines.TakenOverAt,
        };
        var name = FixtureRecipes.All.SingleOrDefault(r => r.Id == recipe.Id.Value)?.Name is null ? null : row.Subject.Label;
        return fields with { Text = StatusText.Render(fields, Now, name) };
    }

    /// <summary>WF section 6.4's state slot: the reserved engine word while the engine owns the instance, else the recipe state.</summary>
    private static string HeaderState(InstanceSummary row) => row.EngineOwned?.Kind switch
    {
        EngineOwnedKind.Parked => "parked",
        EngineOwnedKind.Escalated => "escalated",
        EngineOwnedKind.TakenOver => "taken-over",
        null => row.State,
        _ => throw new InvalidOperationException("no such engine-owned kind"),
    };

    private static List<StatusCommand> Commands(InstanceSummary row, StatusAnswer? answer, OpenWork open)
    {
        var commands = new List<StatusCommand>();
        foreach (var verb in new[] { "answer", "complete", "handback", "takeover", "cancel", "approve" })
        {
            var found = row.Actions.Where(a => VerbOf(a.Value) == verb).ToList();
            if (found.Count == 0)
            {
                continue;
            }

            var (actionClass, cli, confirm) = verb switch
            {
                "cancel" => (ActionClass.Desk, $"sindri workflow cancel {row.Address.Value}", ConfirmText.Cancel(row.Address, open.RunningSteps, open.OpenChildren)),
                "answer" => (answer!.Class, $"sindri workflow answer {row.Address.Value} {VariantOption(found[0].Value)}", null),   // the runnable form: the status line may keep its hint (--resume [--text <answer>])
                "complete" => (ActionClass.Desk, open.CompleteCli!, null),
                "takeover" => (ActionClass.Pause, $"sindri workflow takeover {row.Address.Value}", null),
                "handback" => (ActionClass.Desk, $"sindri workflow handback {row.Address.Value}", null),
                "approve" => (ActionClass.Protected, $"sindri recipe approve {Pinned(row)}", null),
                _ => throw new InvalidOperationException($"no command line for the verb {verb}"),
            };
            commands.Add(new StatusCommand(Enum.Parse<StatusVerb>(verb, ignoreCase: true), found[0], actionClass, cli, confirm));
            foreach (var more in verb == "answer" ? found.Skip(1) : [])   // the engine serves one entry per fitting answer variant
            {
                commands.Add(new StatusCommand(StatusVerb.Answer, more, VariantClass(more.Value), $"sindri workflow answer {row.Address.Value} {VariantOption(more.Value)}", Confirm: null));
            }
        }

        return commands;
    }

    private static string VariantOption(string action) => action switch
    {
        "ApplyCommand:answer-text" => "--text <answer>",
        "ApplyCommand:answer-resume" => "--resume",
        "ApplyCommand:answer-reject" => "--reject",
        "ApplyCommand:answer-grant" => "--grant 1",
        "ApplyCommand:answer-grant-tokens" => "--grant-tokens 100k",
        "ApplyCommand:answer-accept" => "--accept",
        _ => throw new InvalidOperationException($"{action} is not an answer variant"),
    };

    private static ActionClass VariantClass(string action) =>
        action is "ApplyCommand:answer-grant" or "ApplyCommand:answer-grant-tokens" or "ApplyCommand:answer-accept" ? ActionClass.Protected : ActionClass.Desk;

    private static string? VerbOf(string action) => action switch
    {
        "CompleteStep" => "complete",
        "ApproveRecipe" => "approve",
        _ when action.StartsWith("ApplyCommand:answer", StringComparison.Ordinal) => "answer",
        _ when action.StartsWith("ApplyCommand:", StringComparison.Ordinal) => action["ApplyCommand:".Length..],
        _ => null,
    };

    /// <summary>The answer a park takes, with the catalog's action id and class for its kind (grant, grantTokens and accept are protected, at a terminal).</summary>
    private static StatusAnswer Answer(AnswerKind kind, string address, string? arguments = null) => kind switch
    {
        AnswerKind.Grant => new(kind, Act("ApplyCommand:answer-grant"), ActionClass.Protected, $"sindri workflow answer {address} {arguments ?? "--grant 1"}", AtTerminal: true),
        AnswerKind.GrantTokens => new(kind, Act("ApplyCommand:answer-grant-tokens"), ActionClass.Protected, $"sindri workflow answer {address} {arguments ?? "--grant-tokens 100k"}", AtTerminal: true),
        AnswerKind.Accept => new(kind, Act("ApplyCommand:answer-accept"), ActionClass.Protected, $"sindri workflow answer {address} {arguments ?? "--accept"}", AtTerminal: true),
        AnswerKind.Resume => new(kind, Act("ApplyCommand:answer-resume"), ActionClass.Desk, $"sindri workflow answer {address} {arguments ?? "--resume"}", AtTerminal: false),
        AnswerKind.Text => new(kind, Act("ApplyCommand:answer-text"), ActionClass.Desk, $"sindri workflow answer {address} {arguments ?? "--text <answer>"}", AtTerminal: false),
        AnswerKind.CancelOnly => throw new ArgumentOutOfRangeException(nameof(kind), kind, "no answer for this kind"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "no answer for this kind"),
    };

    private static StatusAnswer ChildPointer(string address, string? child) =>
        new(AnswerKind.Text, Act("ApplyCommand:answer-text"), ActionClass.Desk, $"sindri workflow answer {address} <the child's answer>", AtTerminal: false, Via: "child", Child: child);

    /// <summary>What the cancel sentence and the complete command need from the instance's open work; N and M come from the fixture until engine P4b.</summary>
    /// <param name="RunningSteps">The open run's running steps (N).</param>
    /// <param name="OpenChildren">The open children (M).</param>
    /// <param name="CompleteCli">The complete command, when a desk is open.</param>
    private sealed record OpenWork(int RunningSteps = 0, int OpenChildren = 0, string? CompleteCli = null);

    /// <summary>The layout's optional lines.</summary>
    private sealed record StatusLines
    {
        public StatusAnswer? Answer { get; init; }

        public StatusChild? Child { get; init; }

        public StatusDrift? Drift { get; init; }

        public IReadOnlyList<StatusInput> Inputs { get; init; } = [];

        public StatusTimeout? Timeout { get; init; }

        public StatusBudget? Budget { get; init; }

        public StatusPaused? Paused { get; init; }

        public DateTimeOffset? TakenOverAt { get; init; }

        public StatusRun? Run { get; init; }
    }

    private static readonly StatusInput _alertInput = new("fact:alert", "alert.json", Hash("1c0e5a77"));

    // An open desk task: the declared timeout (at least the desk floor, ending in a park) and the state's budget, where the recipe declares one.
    private static StatusFields DeskStatus(InstanceSummary row, string produce, string run, (string Key, string Sample)? output, StatusLines lines, bool lineBreak = false)
    {
        var complete = $"sindri workflow complete {row.Address.Value} --run {run} --outcome done" + (output is { } o ? $" --output {o.Key}={o.Sample}" : string.Empty);
        return Status(row, "desk task", $"{produce}, then run:{(lineBreak ? "\n" : " ")}{complete}", new OpenWork(CompleteCli: complete), lines);
    }

    private static StatusLines DeskLines(StatusInput input, long timeoutSeconds, string state, StatusBudget? budget = null) =>
        new() { Inputs = [input], Timeout = new StatusTimeout(TimeoutKind.Parks, timeoutSeconds, state), Budget = budget };

    /// <summary>Layout 1: a park that mirrors a parked child; the grant is forwarded to it.</summary>
    public static StatusFields StatusParkedChild() => Status(_itemParked, "no-progress (child)",
        "Parked (no-progress) in ITM-01JC8/item/1/step.4: checks failed twice with the same findings. Grant more rounds at a terminal, or cancel.",
        new OpenWork(OpenChildren: 1),
        new StatusLines { Answer = Answer(AnswerKind.Grant, "ITM-01JC8/item/1"), Child = new StatusChild(WorkflowInstanceId.Of(InstanceAddress.From("ITM-01JC8/item/1/step.4")), InstanceAddress.From("ITM-01JC8/item/1/step.4"), "parked", "no-progress") });

    /// <summary>The same park as <see cref="StatusParkedChild"/> when the live lookup of the child gets no answer (engine P7, Q2): the child shows as `unreachable` with no reason, never as its last known state.</summary>
    public static StatusFields StatusUnreachableChild() => Status(_itemParked, "no-progress (child)",
        "Parked (no-progress) in ITM-01JC8/item/1/step.4: checks failed twice with the same findings. Grant more rounds at a terminal, or cancel.",
        new OpenWork(OpenChildren: 1),
        new StatusLines { Answer = Answer(AnswerKind.Grant, "ITM-01JC8/item/1"), Child = new StatusChild(WorkflowInstanceId.Of(InstanceAddress.From("ITM-01JC8/item/1/step.4")), InstanceAddress.From("ITM-01JC8/item/1/step.4"), "unreachable", Reason: null) });

    /// <summary>An escalation from a child: the status answer is a pointer to the child (`via: "child"`), and no answer entry is served.</summary>
    public static StatusFields StatusEscalatedChild() => Status(_itemChildPointer, "escalated (child)",
        "Escalated in ITM-01JC9/item/1/step.4: the child's gate escalated.",
        new OpenWork(OpenChildren: 1),
        new StatusLines { Answer = ChildPointer("ITM-01JC9/item/1", "ITM-01JC9/item/1/step.4"), Child = new StatusChild(WorkflowInstanceId.Of(InstanceAddress.From("ITM-01JC9/item/1/step.4")), InstanceAddress.From("ITM-01JC9/item/1/step.4"), "escalated", "gate") });

    /// <summary>A park mirroring a child's budget park: the cap is the child's, so the status answer is a pointer to the child.</summary>
    public static StatusFields StatusMirroredBudget() => Status(_itemChildPointer, "budget (child)",
        "Parked (budget) in ITM-01JC9/item/1/step.4: the child used 50k of 50k tokens.",
        new OpenWork(OpenChildren: 1),
        new StatusLines { Answer = ChildPointer("ITM-01JC9/item/1", "ITM-01JC9/item/1/step.4"), Child = new StatusChild(WorkflowInstanceId.Of(InstanceAddress.From("ITM-01JC9/item/1/step.4")), InstanceAddress.From("ITM-01JC9/item/1/step.4"), "parked", "budget") });

    /// <summary>The shape today's control plane serves for a mirrored park: the pointer, with no child address (the served view does not list its children yet).</summary>
    public static StatusFields StatusMirroredBudgetNoChild() => Status(_itemChildPointer, "budget (child)",
        "Parked (budget) in the child: the child used 50k of 50k tokens.",
        new OpenWork(OpenChildren: 1),
        new StatusLines { Answer = ChildPointer("ITM-01JC9/item/1", child: null) });

    /// <summary>Layout 2: an open desk task with its inputs, a parking timeout and the state and instance budgets.</summary>
    public static StatusFields StatusDeskTask() => Status(_sentry8812, "desk task",
        "Classify the severity for sentry-8812, then run: sindri workflow complete ITM-01JK2/triage-alert/1 --run 2.1 --outcome done --output fact:severity=<file>",
        new OpenWork(CompleteCli: "sindri workflow complete ITM-01JK2/triage-alert/1 --run 2.1 --outcome done --output fact:severity=<file>"),
        new StatusLines
        {
            Inputs = [_alertInput],
            Timeout = new StatusTimeout(TimeoutKind.Parks, 172_560, "classify"),
            Budget = new StatusBudget("state", 0, 200_000, new BudgetFigures(0, 400_000)),
        });

    /// <summary>Layout 3: a budget park; the answer grants tokens.</summary>
    public static StatusFields StatusParkedBudget() => Status(_budgetStep, "budget", "Parked (budget): produce used 200k of 200k tokens.",
        new OpenWork(), new StatusLines { Answer = Answer(AnswerKind.GrantTokens, "ITM-01JH5/step/1"), Budget = new StatusBudget("state", 200_000, 200_000, new BudgetFigures(200_000, 400_000)) });

    /// <summary>Layout 4: an escalated gate; the answer accepts at a terminal.</summary>
    public static StatusFields StatusEscalatedGate() => Status(_p2211, "gate",
        "Escalated: gate.severity scored 0.62, below 0.8. Accept to override it (the other checks still run before done), or reject (the Step re-runs within its loop, otherwise this parks).",
        new OpenWork(), new StatusLines { Answer = Answer(AnswerKind.Accept, "ITM-01JK6/triage-alert/1") });

    /// <summary>Layout 5: a taken-over instance with its timeout paused.</summary>
    public static StatusFields StatusTakenOver() => Status(_takenOver, null,
        "With you since 12:01. Run `sindri workflow handback ITM-01JA1/step/1` when you're done.", new OpenWork(),
        new StatusLines { Paused = new StatusPaused(40), TakenOverAt = _takenOver.Since, Budget = new StatusBudget("state", 40_000, 200_000, new BudgetFigures(40_000, 400_000)) });

    /// <summary>Layout 6: a finished instance, the header only.</summary>
    public static StatusFields StatusDone() => Status(_sentry8809, "resolved", null, new OpenWork());

    /// <summary>P8: an open Step run on an agent session. The status names the runtime and the launch, offers takeover and cancel, and has no desk task.</summary>
    public static StatusFields StatusAgentRun() => Status(_agentRun, "agent running",
        "An agent session is working on produce. It reports when it is done; take it over to work on it yourself.", new OpenWork(RunningSteps: 1),
        new StatusLines { Run = new StatusRun("agent", "LCH-01JM3A4B5C6D7E8F") });

    /// <summary>A park with a recorded drift: it takes no answer (API Q-API-4): the recipe re-approval is its own protected command, and the approval resumes the instance.</summary>
    public static StatusFields StatusParkedDrift() => Status(_prjCache, "spec-conflict (drift)",
        $"Block step.scope.draft changed since {Pinned(_prjCache)} was approved. Re-approve it (`sindri recipe approve {Pinned(_prjCache)}`); the approval resumes this instance.", new OpenWork(),
        new StatusLines { Drift = new StatusDrift(BlockId.From("step.scope.draft"), NeedsReapproval: true) });

    private static string Pinned(InstanceSummary row) => $"{row.Recipe.Id.Value}@{row.Recipe.Hash.Display}";

    /// <summary>A needs-answer park at a Step on a project of its own: the answer is its text and the Step runs again (WF 6.4; the engine's Step park wording).</summary>
    public static StatusFields StatusNeedsAnswerStep() => Status(_prjReports, "needs-answer",
        "Parked (needs-answer): import the design output, then answer.\nAnswer with --text; the Step runs again with your answer.", new OpenWork(),
        new StatusLines { Answer = Answer(AnswerKind.Text, "ITM-01JKE/project/1", "--text <answer>") });

    /// <summary>A needs-answer park at a wait (dev-triage-three-state's 'answer'): the answer resumes it, with an optional text (WF 6.4).</summary>
    public static StatusFields StatusNeedsAnswerWait() => Status(_triageAnswer, "needs-answer",
        "Parked: the reply to 'Accept the draft?' was not understood.\nFix the cause, then resume to check again, or cancel.", new OpenWork(),
        new StatusLines { Answer = Answer(AnswerKind.Resume, "ITM-01JL4/dev-triage-three-state/1", "--resume [--text <answer>]") });

    /// <summary>Every status the fixtures serve: the eight layouts, then the status block of each instance view.</summary>
    /// <returns>The statuses.</returns>
    public static IReadOnlyList<StatusFields> AllStatuses() =>
    [
        StatusParkedChild(), StatusUnreachableChild(), StatusEscalatedChild(), StatusMirroredBudget(), StatusMirroredBudgetNoChild(), StatusDeskTask(), StatusParkedBudget(), StatusEscalatedGate(), StatusTakenOver(), StatusDone(),
        StatusParkedDrift(), StatusNeedsAnswerStep(), StatusNeedsAnswerWait(), .. AllDetails().Select(d => d.Current.Status),
    ];

    /// <summary>Every instance view the fixtures serve.</summary>
    /// <returns>The views.</returns>
    public static IReadOnlyList<InstanceDetail> AllDetails() =>
        [ProjectBuild(), ProjectManyChildren(), IncidentLoop(), TriageDesk(), EscalatedGate(), ItemProduce(), DeskRoundTrip(), TriageThreeState(), TransitionList(), Done()];


    // ---- facts and children ------------------------------------------------------------------------------------------

    private static FactRef Ref(string? name = null, long? size = null, string? hash = null, string? path = null, string? item = null,
        string? url = null, string? host = null, string? title = null, string? factKey = null) =>
        new(name, size, hash, path, item is null ? null : ItemKey.From(item), url, host, title, factKey);

    // A child is addressed under its parent ('ITM-01JK3/project/1/step.1'); its state is a real state of its recipe (workflow.step), and what it
    // offers follows that state: a child not started or done offers OpenItem only, a parked one shows its park on the parent's band.
    private static ChildRow Child(string parent, int entry, string item, string title, StateGlyph glyph, string state, EngineOwnedBadge? owned = null, long age = 600)
    {
        var parked = owned is { Kind: EngineOwnedKind.Parked };
        var actions = glyph != StateGlyph.Active ? Acts("OpenItem")
            : parked ? Acts("OpenItem", "ApplyCommand:answer-grant", "ApplyCommand:takeover")
            : Acts("OpenItem", "ApplyCommand:takeover");
        // A workflow.step state is a desk task until the agent runtime (P8): an active child that isn't parked is a DECISION with no agent.
        var chip = StateChip.For(owned, glyph == StateGlyph.Active && !parked, state == "done" ? OutcomeName.From("resolved") : null, notStarted: glyph == StateGlyph.Pending);
        var address = InstanceAddress.From(parent + "/step." + entry.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return new(WorkflowInstanceId.Of(address), address, ItemKey.From(item), title, glyph, state, chip, null, null, age, owned, parked ? owned : null, actions);
    }

    private static ViewCounts Counts(int timeline, int decisions, int requests) => new(timeline, decisions, requests, null);

    // ---- the instance views (E-20) -----------------------------------------------------------------------------------

    private static InstanceDetail Detail(InstanceSummary row, string title, PathOrdering ordering, CurrentState current, IReadOnlyList<FactRow> facts,
        IReadOnlyList<ChildGroup> children, ViewCounts counts, ParentRef? parent = null, IReadOnlyList<StepRun>? stepRuns = null, IReadOnlyList<StateStep>? steps = null) =>
        new(row.Address, row.Id, parent, row.ItemId, title, row.Recipe, row.State, row.Since, row.EngineOwned, row.Subject, ordering, row.States, current, facts, children, counts, row.Actions, stepRuns ?? [], Now, steps ?? []);

    /// <summary>One Step state's card: what it reads and the facts leaving it records, each only by key (nothing recorded yet).</summary>
    private static StateStep Card(string state, string[] needs, params string[] produces) => new(state, needs, [.. produces.Select(static k => Ref(factKey: k))]);

    private static FactRow Fake(RecipeShape recipe, string key, FactStatus status, string summary, DateTimeOffset? at = null, string? by = null, FactRef? reference = null, Progress? progress = null, FactPending? pending = null) =>
        recipe.Fact(key, status, summary, at, by, reference, progress, pending);

    private static FactRow Produced(RecipeShape recipe, string key, string summary, DateTimeOffset at, string by, FactRef? reference = null) =>
        recipe.Fact(key, FactStatus.Produced, summary, at, by, reference);

    private static FactRow[] ProjectFacts(DateTimeOffset prdAt, FactRow? evidence = null) =>
    [
        Produced(_project, "fact:project", "Linear APP · Offline sync", prdAt, "human:cli",
            Ref(url: "https://linear.app/example/project/offline-sync", host: "linear.app", title: "APP · Offline sync")),
        Produced(_project, "fact:prd", "PRD-sync.md", prdAt, "human:cli", Ref(name: "PRD-sync.md", size: 18_432, hash: Hash("1c0e5a77"), path: "docs/PRD-sync.md")),
        Produced(_project, "fact:scope", "4 areas, 9 cited files", At(10, 12, 9, 40), "step.scope.draft", Ref(factKey: "fact:scope")),
        Produced(_project, "fact:packet", "9 tasks in 4 workstreams, frozen", At(10, 12, 10, 5), "step.packet.freeze", Ref(factKey: "fact:packet")),
        .. evidence is null ? (FactRow[])[] : [evidence],
    ];

    // PRJ-docs has its own project, PRD, scope map and packet: 24 tasks, frozen, with no workstreams (so the children list is flat).
    private static FactRow[] HandbookFacts(FactRow evidence) =>
    [
        Produced(_project, "fact:project", "Linear DOCS · Handbook", Today(8, 40), "human:cli",
            Ref(url: "https://linear.app/example/project/handbook", host: "linear.app", title: "DOCS · Handbook")),
        Produced(_project, "fact:prd", "PRD-handbook.md", Today(8, 40), "human:cli", Ref(name: "PRD-handbook.md", size: 9_216, hash: Hash("6be7a413"), path: "docs/PRD-handbook.md")),
        Produced(_project, "fact:scope", "3 areas, 24 cited files", Today(8, 55), "step.scope.draft", Ref(factKey: "fact:scope")),
        Produced(_project, "fact:packet", "24 tasks, no workstreams, frozen", Today(9, 5), "step.packet.freeze", Ref(factKey: "fact:packet")),
        evidence,
    ];

    /// <summary>PRJ-sync: build, four of nine children done in four workstreams, a scope map, a packet and evidence; one child parked.</summary>
    public static InstanceDetail ProjectBuild()
    {
        var status = Status(_prjSync, null, "Waiting on 9 children: 4 done, 1 running, 1 parked, 3 not started.", new OpenWork(OpenChildren: 5),
            new StatusLines
            {
                Timeout = new StatusTimeout(TimeoutKind.Escalates, 74_880, "build"),
                Budget = new StatusBudget("state", 1_200_000, 4_000_000, new BudgetFigures(1_900_000, 6_000_000)),
            });
        const string parent = "ITM-01JK3/project/1";
        var ws1 = new Workstream("ws-1", "UI",
            [Child(parent, 1, "ITM-01JC3", "Sync status pill", StateGlyph.Done, "done"), Child(parent, 2, "ITM-01JC5", "Conflict resolver dialog", StateGlyph.Done, "done")]);
        var ws2 = new Workstream("ws-2", "Store",
            [Child(parent, 3, "ITM-01JC1", "PUT /sync/changes", StateGlyph.Done, "done"), Child(parent, 4, "ITM-01JC2", "Outbox queue", StateGlyph.Done, "done"),
                Child(parent, 5, "ITM-01JD6", "Services page job panel", StateGlyph.Active, "checks", age: 0)]);
        var park = new EngineOwnedBadge(EngineOwnedKind.Parked, "no-progress");
        var ws3 = new Workstream("ws-3", "Mobile",
            [Child(parent, 6, "ITM-01JD7", "Background sync (iOS)", StateGlyph.Active, "produce", owned: park), Child(parent, 7, "ITM-01JF3", "doctor views line", StateGlyph.Pending, "produce", age: 3_120),
                Child(parent, 8, "ITM-01JF4", "Tray badge on Linux", StateGlyph.Pending, "produce", age: 2_400)]);
        var ws4 = new Workstream("ws-4", "Rollout", [Child(parent, 9, "ITM-01JF8", "Receipt verify route", StateGlyph.Pending, "produce", age: 720)]);
        return Detail(_prjSync, "Offline sync", PathOrdering.Path,
            new CurrentState("build", new StepDeclaration("wait", "workflow.step", "one child per packet task"),
                [new GateLine(BlockId.From("gate.evidence.all"), "exit", null, "Every child has its evidence pack."), new GateLine(BlockId.From("gate.judge.verify"), "exit", 0.8, "The judge scores the build at least 0.8.")],
                null, status),
            _project.Facts([.. ProjectFacts(At(10, 12, 9, 14), Fake(_project, "fact:evidence", FactStatus.InProgress, "4 of 9 packs", reference: Ref(factKey: "fact:evidence"), progress: new Progress(4, 9)))]),
            [new ChildGroup("build", RecipeId.From("workflow.step"), 4, 9, [ws1, ws2, ws3, ws4], [], 0, "Step")],
            Counts(41, 3, 1));
    }

    private static readonly InstanceSummary _prjDocs = Row("ITM-01JKG/project/1", _project, "build", Today(8, 40), null,
        Subject("linear-project", "DOCS", "DOCS · Handbook"),
        _project.Cells("build", change: Shape(("build", c => c with { Children = new Progress(6, 24) }))),
        "2 running · 16 not started", "ApplyCommand:takeover", "ApplyCommand:cancel");

    /// <summary>PRJ-docs: 24 children, the first 20 listed (no workstreams), four more behind the 'more' count.</summary>
    public static InstanceDetail ProjectManyChildren()
    {
        const string parent = "ITM-01JKG/project/1";
        var rows = Enumerable.Range(1, 20).Select(i => Child(parent, i, $"ITM-01M{i.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)}", "Chapter " + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            i <= 6 ? StateGlyph.Done : i <= 8 ? StateGlyph.Active : StateGlyph.Pending, i <= 6 ? "done" : i <= 8 ? "checks" : "produce",
            age: 600 + (i * 60L))).ToList();
        var status = Status(_prjDocs, null, "Waiting on 24 children: 6 done, 2 running, 16 not started.", new OpenWork(OpenChildren: 18),
            new StatusLines
            {
                Timeout = new StatusTimeout(TimeoutKind.Escalates, 72_840, "build"),
                Budget = new StatusBudget("state", 900_000, 4_000_000, new BudgetFigures(1_100_000, 6_000_000)),
            });
        return Detail(_prjDocs, "Handbook", PathOrdering.Path,
            new CurrentState("build", new StepDeclaration("wait", "workflow.step", "one child per packet task"), [], null, status),
            _project.Facts([.. HandbookFacts(Fake(_project, "fact:evidence", FactStatus.InProgress, "6 of 24 packs", reference: Ref(factKey: "fact:evidence"), progress: new Progress(6, 24)))]),
            [new ChildGroup("build", RecipeId.From("workflow.step"), 6, 24, [], rows, 4, "Step")],
            Counts(30, 0, 0));
    }

    /// <summary>INC-14: the mitigate and verify loop at round 1 of 3, a recorded sev2 outcome, a running comms child, a timeout that escalates.</summary>
    public static InstanceDetail IncidentLoop()
    {
        var status = Status(_inc14, null, "Mitigate the outage in round 1/3: ITM-01JG2 rolls back the last release.", new OpenWork(RunningSteps: 1),
            new StatusLines { Timeout = new StatusTimeout(TimeoutKind.Escalates, 9_360, "mitigate") });
        return Detail(_inc14, "Outbox retry storm", PathOrdering.Path,
            new CurrentState("mitigate", new StepDeclaration("active", "step.agent", "roll back the last release"), [], null, status),
            _incident.Facts(
                Produced(_incident, "fact:alert", "PagerDuty P-2211 · Outbox retry storm", At(10, 12, 11, 2), "service:webhook", Ref(factKey: "fact:alert")),
                Produced(_incident, "fact:severity", "sev2", At(10, 12, 11, 4), "step.assess", Ref(factKey: "fact:severity")),
                Produced(_incident, "fact:history", "2 of 40 events", At(10, 12, 11, 3), "service:webhook", Ref(factKey: "fact:history"))),
            [], Counts(12, 1, 0));
    }

    /// <summary>sentry #8812: the classify desk is open and asks for a severity.</summary>
    public static InstanceDetail TriageDesk()
    {
        var pending = new FactPending(true, ["output.nonempty"], Act("CompleteStep"));
        return Detail(_sentry8812, "TypeError in tabs.layout", PathOrdering.Path,
            new CurrentState("classify", new StepDeclaration("active", "step.classify", "a person classifies the alert"),
                [], new DeskForm(RunToken.From("2.1"), [new DeskOutput("fact:severity", FactType.Text, true)], ["output.nonempty"], Act("CompleteStep")), StatusDeskTask()),
            _triage.Facts(
                Produced(_triage, "fact:alert", "sentry #8812 TypeError in tabs.layout", At(10, 12, 12, 22), "service:webhook", Ref(factKey: "fact:alert")),
                Fake(_triage, "fact:severity", FactStatus.Pending, "awaiting your answer", pending: pending)),
            [], Counts(3, 0, 1));
    }

    /// <summary>pagerduty P-2211: escalated at the severity gate; the accept needs a terminal.</summary>
    public static InstanceDetail EscalatedGate() =>
        Detail(_p2211, "PagerDuty P-2211", PathOrdering.Path,
            new CurrentState("route", new StepDeclaration("active", "step.agent", "route by severity"),
                [new GateLine(BlockId.From("gate.severity"), "exit", 0.8, "The severity check passes at 0.8 or above; below it the instance escalates.")], null, StatusEscalatedGate()),
            _triage.Facts(
                Produced(_triage, "fact:alert", "PagerDuty P-2211", At(10, 12, 11, 2), "service:webhook", Ref(factKey: "fact:alert")),
                Produced(_triage, "fact:severity", "sev1", At(10, 12, 11, 3), "step.classify"),
                Fake(_triage, "fact:route", FactStatus.InProgress, "gate.severity 0.62, below 0.8: escalated")),
            [], Counts(6, 1, 0));

    /// <summary>
    /// ITM-01JE7 (the screens fixture's item): the workflow.step child of its workflow.item instance, at its produce desk task, round 2 of 3
    /// (the second entry of produce, so run 3.1). The recipe's 'outputs:' list is empty, so its fact sheet shows only the inputs; the Step's report
    /// contract is the shipped step.produce's, and the desk form and the complete command carry its required fact:output (WF 5.5).
    /// </summary>
    public static InstanceDetail ItemProduce()
    {
        var row = Row("ITM-01JE7/item/1/step.4", _step, "produce", Today(12, 20), null, Subject("text", "Linear ENG-511 · repo agentic-workflow ·", "Linear ENG-511 · repo agentic-workflow · no project · feature:needs-you.ack"),
            _step.Cells("produce", round: 1, Shape(("produce", c => c with { Round = new Round(2, 3) }))),
            "your output for the Step (desk) · round 2/3", "CompleteStep", "ApplyCommand:takeover", "ApplyCommand:cancel");
        var status = DeskStatus(row, "Produce the Step output for ITM-01JE7 (round 2/3)", "3.1", ("fact:output", "<file>"),
            DeskLines(new StatusInput("fact:spec", "spec.md", Hash("1c0e5a77")), 172_440, "produce", new StatusBudget("state", 0, 200_000, new BudgetFigures(0, 400_000))));
        return Detail(row, "Add needs-you ack route", PathOrdering.Path,
            new CurrentState("produce", new StepDeclaration("active", "step.produce", "produce the output for the item"), [],
                new DeskForm(RunToken.From("3.1"), [new DeskOutput("fact:output", FactType.File, true)], ["output.file"], Act("CompleteStep")), status),
            _step.Facts(Produced(_step, "fact:spec", "spec.md", At(10, 12, 12, 20), "service:item", Ref(name: "spec.md", hash: Hash("1c0e5a77")))),
            [], Counts(6, 0, 0),
            new ParentRef(WorkflowInstanceId.Of(InstanceAddress.From("ITM-01JE7/item/1")), InstanceAddress.From("ITM-01JE7/item/1"), "ITM-01JE7 · Add needs-you ack route", null, null),
            [
                new StepRun("1.1", "produce", Attempt: 1, MaxAttempts: 3, StepRunState.Refused, StepRuntime.Desk, Today(10, 5), Today(10, 50), DurationMs: 2_700_000, Outcome: "done", Tokens: null, Actor: "human:cli"),
                new StepRun("3.1", "produce", Attempt: 2, MaxAttempts: 3, StepRunState.Running, StepRuntime.Desk, Today(12, 20), EndedAt: null, DurationMs: null, Outcome: null, Tokens: null, Actor: null),
            ]);
    }

    /// <summary>The desk-round-trip dev recipe, desk open on its export (round 1 of 2): the P11 signal fixture.</summary>
    public static InstanceDetail DeskRoundTrip()
    {
        var row = Row("ITM-01JL1/dev-desk-round-trip/1", _devDesk, "export", Today(12, 0), null, Subject("text", "Summarize the week", "Summarize the week"),
            _devDesk.Cells("export", round: 0, Shape(("export", c => c with { Round = new Round(1, 2) }))), "next step", "CompleteStep", "ApplyCommand:takeover", "ApplyCommand:cancel");
        return Detail(row, "Desk round trip", PathOrdering.Path,
            new CurrentState("export", new StepDeclaration("active", "step.produce", "a person exports the file"),
                [], new DeskForm(RunToken.From("1.1"), [new DeskOutput("fact:output", FactType.File, true)], ["output.file"], Act("CompleteStep")),
                DeskStatus(row, "Export the file for the request (round 1/2)", "1.1", ("fact:output", "<file>"), lineBreak: true, lines: DeskLines(new StatusInput("fact:request", "request.txt", "sha256:d2a74649875cf790d548f1dfe2e5526b0632bd722a089ec69f89a129068bd9f1"), 171_240, "export"))),
            _devDesk.Facts(
                Produced(_devDesk, "fact:request", "Summarize the week", At(10, 12, 12, 0), "human:cli"),
                Fake(_devDesk, "fact:output", FactStatus.Pending, "awaiting your file", pending: new FactPending(true, ["output.file"], Act("CompleteStep")))),
            [], Counts(2, 0, 0), steps: [Card("export", [], "fact:output"), Card("import", ["fact:output"], "fact:findings")]);
    }

    /// <summary>The three-state triage dev recipe, at its desk.</summary>
    public static InstanceDetail TriageThreeState()
    {
        var row = Row("ITM-01JL2/dev-triage-three-state/1", _devTriage, "desk", Today(12, 0), null, Subject("text", "Crash on launch", "Crash on launch - 2026-10-12"),
            _devTriage.Cells("desk"), "next step", "CompleteStep", "ApplyCommand:takeover", "ApplyCommand:cancel");
        return Detail(row, "Three-state triage", PathOrdering.Path,
            new CurrentState("desk", new StepDeclaration("active", "step.produce", "a person writes the draft"),
                [], new DeskForm(RunToken.From("1.1"), [new DeskOutput("fact:output", FactType.File, true)], ["output.file"], Act("CompleteStep")),
                DeskStatus(row, "Write the draft for ITM-01JL2", "1.1", ("fact:output", "<file>"), lineBreak: true, lines: DeskLines(new StatusInput("fact:scope", "scope.txt", "sha256:2e04b3fa448da3f273578bebd2c8eca04b716240a009837e1d22735e7873c58f"), 171_240, "desk") with { Inputs = [new StatusInput("fact:scope", "scope.txt", "sha256:2e04b3fa448da3f273578bebd2c8eca04b716240a009837e1d22735e7873c58f"), new StatusInput("fact:item", "item.json", "sha256:b6c9cf9454914db27a93569f823751647192bdf84677126620b7f62b45e063fb")] })),
            _devTriage.Facts(
                Produced(_devTriage, "fact:scope", "Crash on launch", At(10, 12, 12, 0), "human:cli"),
                Produced(_devTriage, "fact:item", "ITM-01JL2 Crash on launch", At(10, 12, 12, 0), "human:cli", Ref(item: "ITM-01JL2")),
                Fake(_devTriage, "fact:output", FactStatus.Pending, "awaiting your file", pending: new FactPending(true, ["output.file"], Act("CompleteStep")))),
            [], Counts(2, 0, 0), steps: [Card("desk", [], "fact:output")]);
    }

    /// <summary>A recipe with two initial paths: the band says transition-list and folds 'right' under 'left' (a branch has no order).</summary>
    public static InstanceDetail TransitionList()
    {
        var row = Row("ITM-01JL3/fork-demo/1", _fork, "right", Today(12, 0), null, Subject("text", "fork-demo", "Fork demo"),
            _fork.Cells("right"), "next step", "ApplyCommand:takeover", "ApplyCommand:cancel");
        return Detail(row, "Fork demo", PathOrdering.TransitionList,
            new CurrentState("right", new StepDeclaration("active", "step.agent", "the right branch"), [], null,
                Status(row, null, "branch.right is running.", new OpenWork(RunningSteps: 1), new StatusLines { Timeout = new StatusTimeout(TimeoutKind.Parks, 2_040, "right") })),
            _fork.Facts(Produced(_fork, "fact:request", "Fork it", At(10, 12, 12, 0), "human:cli")),
            [], Counts(2, 0, 0));
    }

    /// <summary>sentry #8809, finished: a header-only status and no commands.</summary>
    public static InstanceDetail Done() =>
        Detail(_sentry8809, "429s on stream", PathOrdering.Path,
            new CurrentState("done", null, [], null, StatusDone()),
            _triage.Facts(
                Produced(_triage, "fact:alert", "sentry #8809 429s on stream", At(10, 12, 11, 40), "service:webhook", Ref(factKey: "fact:alert")),
                Produced(_triage, "fact:severity", "sev3", At(10, 12, 11, 50), "step.classify"),
                Produced(_triage, "fact:route", "ticket", At(10, 12, 11, 58), "step.route"),
                Produced(_triage, "fact:item", "ITM-01JF0 startable", At(10, 12, 11, 58), "step.route", Ref(item: "ITM-01JF0"))),
            [], Counts(7, 0, 0));

    // ---- the recipe additions (E-22) ---------------------------------------------------------------------------------

    /// <summary>workflow.project: a Linear project input, outputs with their producing states, urgency work.</summary>
    public static RecipeView ProjectRecipe() => _project.View();

    /// <summary>workflow.incident-response: urgency incident and the mitigate and verify loop.</summary>
    public static RecipeView IncidentRecipe() => _incident.View();

    /// <summary>workflow.no-outputs, a fixture-only recipe: no instance names it, and its second input is a real generate input (served required: false). 'outputs: []'.</summary>
    public static RecipeView NoOutputsRecipe() => new(
        RecipeId.From("workflow.no-outputs"), RecipeHash.From("sha256:" + "3c9d51e2".PadRight(64, '0')), 1, Name: null,
        [new RecipeInputView("fact:request", "Request", FactType.Text, "text", true, Generate: null), new RecipeInputView("fact:context", null, FactType.Text, "text", false, "context {start:date}")],
        [], RecipeUrgency.Work, PathOrdering.Path, [new RecipeStateView("intake", 1, null), new RecipeStateView("classify", 2, null), new RecipeStateView("done", 3, null)], [], "No outputs");

    /// <summary>The recipe view of one fixture recipe, by recipe id ('workflow.step'): the same declaration its instances and list rows are built from.</summary>
    /// <param name="id">A recipe id of <see cref="FixtureRecipes.All"/>.</param>
    /// <returns>The view.</returns>
    public static RecipeView Recipe(string id) => FixtureRecipes.All.Single(r => r.Id == id).View();

    // ---- the facts route (GetInstanceFact) ---------------------------------------------------------------------------

    /// <summary>The body behind one fact row, by scenario: scope-map, packet, text, payload or truncated (see <see cref="FactFixtures"/>).</summary>
    /// <param name="scenario">The scenario name.</param>
    /// <returns>The fact view.</returns>
    public static FactView Fact(string scenario) => FactFixtures.Fact(scenario);
}
