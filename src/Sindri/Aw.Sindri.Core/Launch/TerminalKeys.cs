using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Sindri.Core.Launch;

/// <summary>The key names a 'key' message may carry (agents spec §15.2, foreign list).</summary>
public static partial class TerminalKeys
{
    /// <summary>Checks a key name: Enter, Escape, Tab, BTab, BSpace, DC, the arrows, Home, End, PageUp, PageDown, F1 to F12, C-a to C-z, M-a to M-z.</summary>
    /// <param name="key">The name.</param>
    /// <returns>Success, or an invalid error that names the key.</returns>
    public static Result<Unit> Check(string key) =>
        key is not null && Allowed.IsMatch(key)
            ? Unit.Value
            : SindriErrors.InvalidInput.With($"'{key}' is not a key Sindri sends to a pane");

    [GeneratedRegex(@"\A(Enter|Escape|Tab|BTab|BSpace|DC|Up|Down|Left|Right|Home|End|PageUp|PageDown|F([1-9]|1[0-2])|[CM]-[a-z])\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Allowed { get; }
}
