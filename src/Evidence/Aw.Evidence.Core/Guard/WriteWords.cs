using System.Text.RegularExpressions;
using Aw.Evidence.Core.Scripts;

namespace Aw.Evidence.Core.Guard;

/// <summary>
/// Best-effort detection of write controls: a click target (or a goto path, query dropped) whose words include a
/// write verb, alone or fused with a control suffix ('savebutton', 'deleteBtn'). camelCase and separators split words.
/// </summary>
internal static partial class WriteWords
{
    private static readonly HashSet<string> _verbs = new(StringComparer.Ordinal)
    {
        "save", "submit", "update", "delete", "remove", "create", "confirm", "publish", "send", "apply", "approve", "post", "add",
        "invite", "archive", "deactivate", "activate", "enable", "disable", "cancel", "reset", "assign", "import", "upload", "complete",
        "finish", "accept", "reject", "sync", "release", "merge", "logout", "signout", "register",
    };

    // The words a CLI argument may carry on top of the UI list: commands that change or remove something.
    private static readonly HashSet<string> _cliVerbs = new(StringComparer.Ordinal)
    {
        "push", "drop", "rm", "truncate", "destroy", "purge", "wipe", "kill", "commit", "put", "patch", "write", "rollback", "revert", "uninstall",
    };

    private static readonly string[] _controlSuffixes = ["btn", "button", "link", "action"];

    // Keys that move focus or selection, optionally with Shift. Anything else (Enter, Space, a letter, Control+S) can activate a control.
    private static readonly string[] _navigationKeys =
        ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Home", "End", "PageUp", "PageDown", "Tab", "Escape"];

    /// <summary>
    /// A click reads its target's label (the name of a role: target, the text of a text: target, a '#&lt;n&gt;' dropped); a goto reads its path.
    /// Every drag counts as a write: dropping moves data whatever the test ids say. A press counts as a write unless its key only
    /// navigates: Enter or Space on a focused Save button saves, whatever the target says. Scroll and the rest never write.
    /// </summary>
    internal static bool LooksLikeWrite(ScriptStep step) => step.Action switch
    {
        StepAction.Drag => true,
        StepAction.Press => !IsNavigationKey(step.Key),
        StepAction.Click => LabelLooksLikeWrite(LabelOf(step.Target)),
        StepAction.Goto => LabelLooksLikeWrite(step.Target.Split('?')[0]),
        StepAction.Fill or StepAction.ExpectVisible or StepAction.Scroll => false,
        _ => false,
    };

    private static string LabelOf(string target) => StepTarget.Parse(target) is { IsSuccess: true } parsed ? parsed.Value.Label : target;

    private static bool IsNavigationKey(string? key) =>
        key is not null && _navigationKeys.Contains(key.StartsWith("Shift+", StringComparison.Ordinal) ? key["Shift+".Length..] : key, StringComparer.Ordinal);

    /// <summary>The words of a CLI argument that read as write verbs (the UI list plus the CLI verbs).</summary>
    internal static IEnumerable<string> CliWritesIn(string argument) => Words(argument).Where(static w => IsWriteWord(w) || _cliVerbs.Contains(w));

    internal static bool LabelLooksLikeWrite(string? label) => label is not null && Words(label).Any(IsWriteWord);

    internal static IEnumerable<string> Words(string target) =>
        NonWord.Split(CamelBoundary.Replace(target, "${lower} ${upper}").ToLowerInvariant()).Where(static w => w.Length > 0);

    private static bool IsWriteWord(string token) =>
        _verbs.Contains(token)
        || _controlSuffixes.Any(suffix => token.EndsWith(suffix, StringComparison.Ordinal) && _verbs.Contains(token[..^suffix.Length]));

    [GeneratedRegex("(?<lower>[a-z0-9])(?<upper>[A-Z])", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex CamelBoundary { get; }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex NonWord { get; }
}
