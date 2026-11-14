using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One line of a step's output in a compare cell, and whether the other run lacks it.</summary>
/// <param name="Text">The line as printed, indentation kept.</param>
/// <param name="Differs">True when the other run has no such line: it gets a bar and the word "differs".</param>
internal sealed record CompareLineViewModel(string Text, bool Differs);

/// <summary>One run's side of one step.</summary>
internal sealed class CompareCellViewModel
{
    internal CompareCellViewModel()
    {
        Present = false;
        Message = "Not in this run";
    }

    internal CompareCellViewModel(NodeRunView run)
    {
        Present = true;
        var (glyph, word) = Labels.WfChip(run.Status);
        (StatusGlyph, StatusWord, ToneWord) = (glyph, word, RunStatus.ToneWord(run.Status));
        DurationText = run.Seconds is { } s and >= 0 ? (s < 10 ? string.Create(CultureInfo.InvariantCulture, $"{s:0.#} s") : Labels.WfDuration(s)) : string.Empty;
        var output = WorkflowJson.Pretty(run.Output);
        var input = WorkflowJson.Pretty(run.Input);
        (BodyLabel, Text) = output.Length > 0 ? ("Output", output) : input.Length > 0 ? ("Input", input) : (string.Empty, string.Empty);
        Message = run.Status switch
        {
            "failed" => run.Error ?? string.Empty,
            "skipped" => run.Error is { Length: > 0 } why ? "Did not run: " + why : "Did not run",
            _ => string.Empty,
        };
        Status = run.Status;
    }

    internal string Status { get; } = string.Empty;

    internal string Text { get; } = string.Empty;

    /// <summary>False when the run has no such step.</summary>
    public bool Present { get; }

    /// <summary>"Succeeded", "Failed", "Skipped"; empty when the step is not in the run.</summary>
    public string StatusWord { get; } = string.Empty;

    /// <summary>The status glyph.</summary>
    public string StatusGlyph { get; } = string.Empty;

    /// <summary>The chip tone word.</summary>
    public string ToneWord { get; } = "neutral";

    /// <summary>"5.2 s".</summary>
    public string DurationText { get; } = string.Empty;

    /// <summary>"Output" or "Input"; empty when there are no lines.</summary>
    public string BodyLabel { get; } = string.Empty;

    /// <summary>The output (or input) as lines, each marked when the other run lacks it.</summary>
    public IReadOnlyList<CompareLineViewModel> Lines { get; private set; } = [];

    /// <summary>True when there are lines to draw.</summary>
    public bool HasLines => Lines.Count > 0;

    /// <summary>True when there is a message line.</summary>
    public bool HasMessage => Message.Length > 0;

    /// <summary>The one-line message: the error, "Did not run: ...", or "Not in this run".</summary>
    public string Message { get; }

    /// <summary>True when the other run's message differs.</summary>
    public bool MessageDiffers { get; private set; }

    /// <summary>True when the head status row shows (the step is in the run).</summary>
    public bool HasStatus => Present;

    internal void Mark(CompareCellViewModel other)
    {
        var theirs = other.Text.Split('\n').Select(l => l.Trim()).ToHashSet(StringComparer.Ordinal);
        Lines = Text.Length == 0 ? [] : [.. Text.Split('\n').Select(l => new CompareLineViewModel(l, other.Present && !theirs.Contains(l.Trim())))];
        MessageDiffers = !string.Equals(Message, other.Message, StringComparison.Ordinal);
    }
}

/// <summary>One step with both runs' cells.</summary>
internal sealed class CompareStepViewModel
{
    internal CompareStepViewModel(string name, string kind, CompareCellViewModel a, CompareCellViewModel b)
    {
        (Name, Kind, A, B) = (name, kind, a, b);
        a.Mark(b);
        b.Mark(a);
        Differs = a.Present != b.Present || !string.Equals(a.Status, b.Status, StringComparison.Ordinal) || a.MessageDiffers || a.Lines.Any(l => l.Differs) || b.Lines.Any(l => l.Differs);
    }

    /// <summary>The step's id.</summary>
    public string Name { get; }

    /// <summary>The step's kind ("code", "notify").</summary>
    public string Kind { get; }

    /// <summary>The newer run's side.</summary>
    public CompareCellViewModel A { get; }

    /// <summary>The older run's side.</summary>
    public CompareCellViewModel B { get; }

    /// <summary>True when the two sides differ in presence, status, message or a line.</summary>
    public bool Differs { get; }
}

/// <summary>The head of one run's column.</summary>
internal sealed class CompareRunHeadViewModel
{
    internal CompareRunHeadViewModel(string letter, WorkflowRunView run, TimeProvider clock)
    {
        var local = TimeZoneInfo.ConvertTime(run.StartedAt, clock.LocalTimeZone);
        Title = $"Run {letter} \u00b7 " + local.ToString("ddd d MMM, HH:mm", CultureInfo.InvariantCulture);
        var parts = new List<string> { string.Create(CultureInfo.InvariantCulture, $"r{run.Revision}") };
        if (run.EndedAt is { } ended)
        {
            parts.Add(Labels.WfDuration((ended - run.StartedAt).TotalSeconds));
        }

        parts.Add(Labels.WfRunTriggerWords(KindOf(run.Trigger)));
        Facts = string.Join(" \u00b7 ", parts);
        (ChipGlyph, ChipWord) = Labels.WfChip(run.Status);
        ToneWord = RunStatus.ToneWord(run.Status);
        Result = run.Error ?? string.Empty;
    }

    /// <summary>"Run A \u00b7 Sat 14 Nov, 08:40".</summary>
    public string Title { get; }

    /// <summary>"r2 \u00b7 10 s \u00b7 From the CLI".</summary>
    public string Facts { get; }

    /// <summary>The run's status word.</summary>
    public string ChipWord { get; }

    /// <summary>The run's status glyph.</summary>
    public string ChipGlyph { get; }

    /// <summary>The chip tone word.</summary>
    public string ToneWord { get; }

    /// <summary>The run's one-line result.</summary>
    public string Result { get; }

    private static string KindOf(JsonElement? trigger) =>
        trigger is { ValueKind: JsonValueKind.Object } t && t.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String ? kind.GetString() ?? string.Empty : string.Empty;
}

/// <summary>Two runs compared step by step (frame wf-compare).</summary>
internal sealed class CompareViewModel
{
    internal CompareViewModel()
    {
        (A, B) = (null!, null!);
    }

    internal CompareViewModel(WorkflowRunView a, WorkflowRunView b, IReadOnlyDictionary<string, string> kinds, TimeProvider clock)
    {
        (A, B) = (new CompareRunHeadViewModel("A", a, clock), new CompareRunHeadViewModel("B", b, clock));
        var (left, right) = (First(a), First(b));
        var names = left.Keys.Concat(right.Keys.Where(k => !left.ContainsKey(k))).ToList();   // the newer run's steps in its order, then the ones only the older one walked
        Steps = [.. names.Select(n => new CompareStepViewModel(n, kinds.GetValueOrDefault(n, string.Empty), Cell(left, n), Cell(right, n)))];
        var differing = Steps.Count(s => s.Differs);
        SummaryText = differing == 0 ? "the two runs match" : string.Create(CultureInfo.InvariantCulture, $"differs in {differing} of {Steps.Count} {(Steps.Count == 1 ? "step" : "steps")}");
        var running = new[] { ("A", a), ("B", b) }.Where(r => RunStatus.IsLive(r.Item2.Status)).Select(r => r.Item1).ToList();
        RunningNote = running.Count switch
        {
            0 => string.Empty,
            1 => $"Run {running[0]} is still running; its steps so far are shown",
            _ => "Both runs are still running; their steps so far are shown",
        };
    }

    /// <summary>The newer run's head.</summary>
    public CompareRunHeadViewModel A { get; }

    /// <summary>The older run's head.</summary>
    public CompareRunHeadViewModel B { get; }

    /// <summary>The steps, in the order the newer run walked them, then the ones only the older one did.</summary>
    public IReadOnlyList<CompareStepViewModel> Steps { get; } = [];

    /// <summary>"differs in 3 of 3 steps" or "the two runs match".</summary>
    public string SummaryText { get; } = string.Empty;

    /// <summary>"Run A is still running; its steps so far are shown"; empty when neither is.</summary>
    public string RunningNote { get; } = string.Empty;

    /// <summary>True when a run is still running.</summary>
    public bool HasRunningNote => RunningNote.Length > 0;

    private static Dictionary<string, NodeRunView> First(WorkflowRunView run)
    {
        var steps = new Dictionary<string, NodeRunView>(StringComparer.Ordinal);
        foreach (var node in run.Nodes)
        {
            _ = steps.TryAdd(node.Node, node);
        }

        return steps;
    }

    private static CompareCellViewModel Cell(Dictionary<string, NodeRunView> steps, string name) => steps.TryGetValue(name, out var run) ? new CompareCellViewModel(run) : new CompareCellViewModel();
}
