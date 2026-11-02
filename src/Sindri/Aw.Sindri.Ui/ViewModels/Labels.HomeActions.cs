namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): what Home's row says when an action on it sent nothing (#1058).</summary>
internal static partial class Labels
{
    /// <summary>A Complete that could not be read or sent from the app.</summary>
    /// <param name="cli">The command that does it; empty when none is known.</param>
    /// <returns>The sentence for the row.</returns>
    internal static string CompleteInTerminal(string cli) => cli.Length > 0 ? "Can't complete this step from the app. Run: " + cli : "Can't complete this step from the app: its workflow can't be read";

    /// <summary>A refusal of Pause service or Complete, as served.</summary>
    /// <param name="code">The refusal's code.</param>
    /// <param name="detail">The refusal's detail.</param>
    /// <returns>"SND-...: detail".</returns>
    internal static string ActionRefused(string code, string detail) => code + ": " + detail;
}
