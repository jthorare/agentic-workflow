using Aw.Kernel;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// The commands Sindri sends to tmux (agents spec §15.2): a foreign list and a launch list. It limits only what Sindri sends; what the owner types in an attached pane is his.
/// A7 owns the class; A8 adds the terminal verbs' tests.
/// </summary>
public static class TmuxAllowlist
{
    /// <summary>Checks one tmux call, as the arguments after 'tmux -L &lt;socket&gt;'.</summary>
    /// <param name="list">The list the socket's owner decides.</param>
    /// <param name="args">The tmux verb and its arguments.</param>
    /// <returns>Success, or an invalid error that names the refused verb, flag, key or target.</returns>
    public static Result<Unit> Check(TmuxList list, IReadOnlyList<string> args) => throw new NotSupportedException("red: A7 step 2");
}
