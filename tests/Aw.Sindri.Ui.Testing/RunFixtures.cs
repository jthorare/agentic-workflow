using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// A line run (the run canvas's page, F3 spec 3) built from the engine's real instance golden: four stages, the redesign branch and
/// the three ends of workflow.line, with the glyphs and child chips of the state asked for. Nothing here is the prototype's invented data.
/// </summary>
internal static class RunFixtures
{
    internal static readonly string[] Stages = ["scoping", "design", "plan", "phase"];

    /// <summary>The chip a word makes, with the tone the engine gives it.</summary>
    /// <param name="word">The upper-case chip word.</param>
    /// <returns>The chip.</returns>
    internal static StateChip Chip(string word) => new(word, word switch
    {
        "FAILED" => ChipTone.Fail,
        "RUNNING" => ChipTone.Ok,
        "DONE" or "QUEUED" or "CANCELLED" or "WITH YOU" => ChipTone.Neutral,
        _ => ChipTone.Attn,
    });

    /// <summary>A cell of the line's states.</summary>
    /// <param name="id">The state id.</param>
    /// <param name="order">The path order.</param>
    /// <param name="glyph">The glyph.</param>
    /// <param name="child">Whether the state waits on a child.</param>
    /// <param name="branchOf">The state a branch leaves.</param>
    /// <param name="terminal">Whether the state ends the run.</param>
    /// <param name="outcome">The outcome of a terminal state that was reached.</param>
    /// <returns>The cell.</returns>
    internal static StateCell Cell(string id, int? order, StateGlyph glyph, bool child = true, string? branchOf = null, bool terminal = false, string? outcome = null) =>
        new(id, order, branchOf, glyph, Badge: null, Round: null, Loops: [], Desk: false, child, Children: null, outcome, terminal);

    /// <summary>A child row at a stage with a chip.</summary>
    /// <param name="id">The child's instance id.</param>
    /// <param name="state">The stage it belongs to.</param>
    /// <param name="word">The chip word.</param>
    /// <returns>The row.</returns>
    internal static ChildRow Row(string id, string state, string word) =>
        new(id, "ITM-01JN2/" + state + "/1", "ITM-01JN2", state, StateGlyph.Active, state, Chip(word), Round: null, Agent: null, AgeSeconds: 60, EngineOwned: null, MirroredPark: null, Actions: []);

    /// <summary>The line run at a stage, or at an end.</summary>
    /// <param name="at">The line's current state: a stage ("plan") or an end ("done", "stuck", "dropped").</param>
    /// <param name="chips">The chip word of the child of a stage; a stage before <paramref name="at"/> reads DONE, <paramref name="at"/> reads RUNNING, later stages have no child.</param>
    /// <param name="outcome">The outcome of the end when <paramref name="at"/> is one.</param>
    /// <returns>The instance.</returns>
    internal static InstanceDetail Line(string at = "plan", IReadOnlyDictionary<string, string>? chips = null, string? outcome = null)
    {
        var (cells, groups) = StageCells(at, chips);
        cells.Add(Cell("redesign", order: null, glyph: StateGlyph.Pending, branchOf: "plan"));
        cells.Add(End("done", 5, at, outcome ?? "resolved"));
        cells.Add(End("stuck", 6, at, outcome ?? "unresolved"));
        cells.Add(End("dropped", 7, at, outcome ?? "unresolved"));
        var golden = FixtureInstances.Detail("project-build");
        return golden with
        {
            Address = "ITM-01JN2/line/1",
            Id = "WFI-line",
            State = at,
            Recipe = golden.Recipe with { Id = "workflow.line", DisplayName = "Idea to merged pull request" },
            DisplayName = "Idea to merged pull request",
            States = cells,
            Children = groups,
        };
    }

    private static (List<StateCell> Cells, List<ChildGroup> Groups) StageCells(string at, IReadOnlyDictionary<string, string>? chips)
    {
        var current = Array.IndexOf(Stages, at);
        var cells = new List<StateCell>();
        var groups = new List<ChildGroup>();
        for (var i = 0; i < Stages.Length; i++)
        {
            var past = current < 0 || i < current;
            cells.Add(Cell(Stages[i], i + 1, past ? StateGlyph.Done : i == current ? StateGlyph.Active : StateGlyph.Pending));
            if (past || i == current)
            {
                groups.Add(Group(Stages[i], chips is not null && chips.TryGetValue(Stages[i], out var given) ? given : past ? "DONE" : "RUNNING"));
            }
        }

        return (cells, groups);
    }

    private static ChildGroup Group(string stage, string word) =>
        new(stage, "workflow." + stage, Done: 0, Total: 1, Workstreams: [], Rows: [Row("WFI-" + stage, stage, word)], More: 0, DisplayName: char.ToUpperInvariant(stage[0]) + stage[1..]);

    private static StateCell End(string id, int order, string at, string outcome) =>
        Cell(id, order, string.Equals(id, at, StringComparison.Ordinal) ? StateGlyph.Done : StateGlyph.Pending, child: false, terminal: true, outcome: string.Equals(id, at, StringComparison.Ordinal) ? outcome : null);

    /// <summary>The line's recipe as served now: the states and the redesign loop, no transitions.</summary>
    /// <returns>The recipe.</returns>
    internal static RecipeView LineRecipe() =>
        FixtureInstances.Recipe("workflow.project") with
        {
            Id = "workflow.line",
            DisplayName = "Idea to merged pull request",
            Loops = [new RecipeLoopView("redesign", "redesign", "plan", 2)],
        };

    /// <summary>A stage's child in full, with the steps asked for.</summary>
    /// <param name="stage">The stage.</param>
    /// <param name="steps">Each step's id, glyph and whether it waits at the desk.</param>
    /// <returns>The child instance.</returns>
    internal static InstanceDetail Child(string stage, params (string Id, StateGlyph Glyph, bool Desk)[] steps)
    {
        var golden = FixtureInstances.Detail("project-build");
        var cells = steps.Select((s, i) => Cell(s.Id, i + 1, s.Glyph, child: false) with { Desk = s.Desk }).ToList();
        cells.Add(Cell("done", steps.Length + 1, StateGlyph.Pending, child: false, terminal: true));
        return golden with { Address = "ITM-01JN2/" + stage + "/1", Id = "WFI-" + stage, States = cells, StepRuns = [] };
    }
}
