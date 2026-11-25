namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>One line under the System page's Agents card counts (#4012): a session problem in words and its code.</summary>
/// <param name="Text">The problem's sentence, as served.</param>
/// <param name="Code">Its SND code, for example SND-SESSION-001.</param>
internal sealed record AgentProblemLine(string Text, string Code)
{
    /// <summary>The code as drawn: its hyphens are non-breaking (U+2011), so a wrap at a large text scale can't leave "SND-" on one line (#4269).</summary>
    public string ShownCode { get; } = Code.Replace('-', '‑');
}
