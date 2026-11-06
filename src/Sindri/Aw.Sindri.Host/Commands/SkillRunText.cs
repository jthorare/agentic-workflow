namespace Aw.Sindri.Host.Commands;

#pragma warning disable MA0182 // until the 'skill run' verb (F-1 green) calls it; the tests pin its text and exit codes

/// <summary>The text and exit code of 'sindri skill run --follow' (skill flows spec §7): exit 0 on the success outcome, 3 on any other mapped outcome, 4 on step.failed.</summary>
internal static class SkillRunText
{
    /// <summary>The text: the manifest hash at the top, the outcome, each fact, the result path, and the SND line at the end.</summary>
    /// <param name="report">How the run ended.</param>
    /// <returns>The text.</returns>
    internal static string Render(SkillRunReport report) => throw new NotSupportedException();

    /// <summary>The exit code.</summary>
    /// <param name="report">How the run ended.</param>
    /// <returns>0, 3 or 4.</returns>
    internal static int ExitCode(SkillRunReport report) => throw new NotSupportedException();
}
#pragma warning restore MA0182
