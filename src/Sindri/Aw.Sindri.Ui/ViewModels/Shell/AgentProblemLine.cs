namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>One line under the System page's Agents card counts (#4012): a session problem in words and its code.</summary>
/// <param name="Text">The problem's sentence, as served.</param>
/// <param name="Code">Its SND code, for example SND-SESSION-001.</param>
internal sealed record AgentProblemLine(string Text, string Code)
{
    /// <summary>The code as drawn, glued so a wrap can't split it at a hyphen (#4269, not built yet).</summary>
    public string ShownCode { get; } = string.Empty;
}
