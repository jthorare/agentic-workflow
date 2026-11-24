using System.Buffers;

namespace Aw.Sindri.Ui.Platform;

/// <summary>Picks the editor command (#3715): AW_SINDRI_EDITOR, then VISUAL, then EDITOR; the first one that is not blank, split on whitespace into an argv list. No shell reads it.
/// The launcher takes the first of them that is not a terminal editor (#4190).</summary>
internal static class EditorCommand
{
    // Editors that draw in a terminal (#3836): the app starts them with none, so nothing shows. Matched ignoring case (Vim.exe, NVIM; #4190).
    private static readonly HashSet<string> _terminal = new(StringComparer.OrdinalIgnoreCase)
    {
        "vim", "vi", "view", "vimdiff", "nvim", "nano", "pico", "micro", "helix", "hx", "kak", "ne", "joe", "mcedit", "ed", "emacs-nox",
    };

    // Emacs is a window unless one of these asks for the terminal.
    private static readonly HashSet<string> _emacs = new(StringComparer.OrdinalIgnoreCase) { "emacs", "emacsclient" };
    private static readonly HashSet<string> _tty = new(StringComparer.Ordinal) { "-nw", "-t", "--tty", "--no-window-system" };

    // emacsclient's short flags that take no value, so they can be joined ("-tc", "-nwc"; #4190).
    private static readonly SearchValues<char> _joinedFlags = SearchValues.Create("cnqtuw");

    /// <summary>True when the command runs a terminal editor (#3836): its program's file name, with or without a path and ignoring case, is a known one,
    /// or Emacs with a terminal flag, alone or joined with other short flags.</summary>
    /// <param name="argv">The command, as <see cref="Candidates"/> yields it.</param>
    /// <returns>True for vim, nvim, nano and the like; false for a window editor.</returns>
    internal static bool IsTerminal(IReadOnlyList<string> argv)
    {
        if (argv.Count == 0)
        {
            return false;
        }

        var name = Path.GetFileName(argv[0]);
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        return _terminal.Contains(name) || (_emacs.Contains(name) && argv.Skip(1).Any(AsksForTerminal));
    }

    // "-nw", "-t", "--tty", "--no-window-system", or a joined group of no-value short flags holding t or nw ("-tc", "-nwc").
    private static bool AsksForTerminal(string flag) =>
        _tty.Contains(flag)
        || (flag.Length > 2 && flag[0] == '-' && flag[1] != '-' && flag.AsSpan(1).IndexOfAnyExcept(_joinedFlags) < 0
            && (flag.Contains('t', StringComparison.Ordinal) || flag.Contains("nw", StringComparison.Ordinal)));

    /// <summary>Every command that is set, in order: AW_SINDRI_EDITOR, VISUAL, EDITOR, each split into an argv list; blank ones skipped.</summary>
    /// <param name="environment">The three settings.</param>
    /// <returns>The commands, first choice first.</returns>
    internal static IEnumerable<IReadOnlyList<string>> Candidates(EditorEnvironment environment)
    {
        foreach (var value in (string?[])[environment.Configured, environment.Visual, environment.Editor])
        {
            var argv = value?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (argv is { Length: > 0 })
            {
                yield return argv;
            }
        }
    }

    /// <summary>The command's argv without the file.</summary>
    /// <param name="environment">The three settings.</param>
    /// <returns>The program and its arguments, or null when none is set.</returns>
    internal static IReadOnlyList<string>? Resolve(EditorEnvironment environment) => Candidates(environment).FirstOrDefault();
}
