namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One line of a step's output in a compare cell, and whether the other run lacks it.</summary>
internal sealed class CompareLineViewModel
{
    /// <summary>The line as printed, indentation kept.</summary>
    public string Text => string.Empty;

    /// <summary>True when the other run has no such line: it gets a bar and the word "differs".</summary>
    public bool Differs => false;
}

/// <summary>One run's side of one step.</summary>
internal sealed class CompareCellViewModel
{
    /// <summary>False when the run has no such step.</summary>
    public bool Present => true;

    /// <summary>"Succeeded", "Failed", "Skipped"; empty when the step is not in the run.</summary>
    public string StatusWord => string.Empty;

    /// <summary>The status glyph.</summary>
    public string StatusGlyph => string.Empty;

    /// <summary>The chip tone word.</summary>
    public string ToneWord => "neutral";

    /// <summary>"5.2 s".</summary>
    public string DurationText => string.Empty;

    /// <summary>"Output" or "Input"; empty when there are no lines.</summary>
    public string BodyLabel => string.Empty;

    /// <summary>The output (or input) as lines.</summary>
    public IReadOnlyList<CompareLineViewModel> Lines => [];

    /// <summary>The one-line message: the error, "Did not run: ...", or "Not in this run".</summary>
    public string Message => string.Empty;

    /// <summary>True when the other run's message differs.</summary>
    public bool MessageDiffers => false;
}

/// <summary>One step with both runs' cells.</summary>
internal sealed class CompareStepViewModel
{
    /// <summary>The step's id.</summary>
    public string Name => string.Empty;

    /// <summary>The step's kind ("code", "notify").</summary>
    public string Kind => string.Empty;

    /// <summary>The newer run's side.</summary>
    public CompareCellViewModel A { get; } = new();

    /// <summary>The older run's side.</summary>
    public CompareCellViewModel B { get; } = new();

    /// <summary>True when the two sides differ in presence, status, message or a line.</summary>
    public bool Differs => false;
}

/// <summary>The head of one run's column.</summary>
internal sealed class CompareRunHeadViewModel
{
    /// <summary>"Run A \u00b7 Sat 14 Nov, 08:40".</summary>
    public string Title => string.Empty;

    /// <summary>"r2 \u00b7 10 s \u00b7 From the CLI".</summary>
    public string Facts => string.Empty;

    /// <summary>The run's status word.</summary>
    public string ChipWord => string.Empty;

    /// <summary>The run's status glyph.</summary>
    public string ChipGlyph => string.Empty;

    /// <summary>The chip tone word.</summary>
    public string ToneWord => "neutral";

    /// <summary>The run's one-line result.</summary>
    public string Result => string.Empty;
}

/// <summary>Two runs compared step by step (frame wf-compare).</summary>
internal sealed class CompareViewModel
{
    /// <summary>The newer run's head.</summary>
    public CompareRunHeadViewModel A { get; } = new();

    /// <summary>The older run's head.</summary>
    public CompareRunHeadViewModel B { get; } = new();

    /// <summary>The steps, in the order the newer run walked them, then the ones only the older one did.</summary>
    public IReadOnlyList<CompareStepViewModel> Steps => [];

    /// <summary>"differs in 3 of 3 steps" or "the two runs match".</summary>
    public string SummaryText => string.Empty;

    /// <summary>"Run A is still running; its steps so far are shown"; empty when neither is.</summary>
    public string RunningNote => string.Empty;
}
