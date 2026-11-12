using Aw.Kernel;

namespace Aw.Sindri.Core.Launch;

/// <summary>The key names a 'key' message may carry (agents spec §15.2, foreign list).</summary>
public static class TerminalKeys
{
    /// <summary>Checks a key name: Enter, Escape, Tab, BTab, BSpace, DC, the arrows, Home, End, PageUp, PageDown, F1 to F12, C-a to C-z, M-a to M-z.</summary>
    /// <param name="key">The name.</param>
    /// <returns>Success, or an invalid error that names the key.</returns>
    public static Result<Unit> Check(string key) => throw new NotSupportedException("red: A8 step 2");
}
