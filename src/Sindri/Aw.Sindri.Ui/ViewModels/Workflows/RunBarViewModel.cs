namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One slot of a list row's strip of the last 10 runs: its outcome, its shape (a failed run is tall, a cancelled one hollow, so colour is never the only cue) and the two tooltip lines.</summary>
internal sealed class RunBarViewModel
{
    /// <summary>The outcome: succeeded, failed, cancelled, running or empty.</summary>
    public string Kind => string.Empty;

    /// <summary>True for a failed run: a tall bar.</summary>
    public bool IsTall => false;

    /// <summary>True for a cancelled run: a hollow bar.</summary>
    public bool IsHollow => false;

    /// <summary>"Run 499 · failed · 22m ago"; empty for an empty slot.</summary>
    public string TitleLine => string.Empty;

    /// <summary>"Sat 14 Nov 2026 · 10:20:18 · 10 s"; empty for an empty slot.</summary>
    public string DetailLine => string.Empty;
}
