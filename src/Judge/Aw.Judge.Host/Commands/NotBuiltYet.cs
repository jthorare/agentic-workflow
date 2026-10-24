namespace Aw.Judge.Host.Commands;

/// <summary>
/// The answer of every verb that isn't in this build yet (cutover spec §5.2): stdout empty, one stderr line
/// 'judge: &lt;verb&gt; is not in this build yet', exit 1. It validates no argument, opens no database, reads no stdin,
/// writes nothing and calls no provider, so it can't fail. Each stubbed verb lands in its own PR, which deletes its route.
/// </summary>
internal static class NotBuiltYet
{
    /// <summary>Says the verb isn't built.</summary>
    /// <param name="verb">The verb as it is named in the line: the first word, plus a second only for 'eval import-turns' and a stubbed 'prompt-sort &lt;sub&gt;'.</param>
    /// <returns>The exit code, 1.</returns>
    internal static int Say(string verb) => CommandOutput.Emit(1, "", $"judge: {verb} is not in this build yet");
}
