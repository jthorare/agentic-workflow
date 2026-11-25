namespace Aw.Sindri.Ui.Platform;

/// <summary>Picks the editor command (#3715): AW_SINDRI_EDITOR, then VISUAL, then EDITOR; the first one that is not blank, split on whitespace into an argv list. No shell reads it.</summary>
internal static class EditorCommand
{
    // Editors that draw in a terminal (#3836): the app starts them with none, so nothing shows.
    private static readonly HashSet<string> _terminal = new(StringComparer.Ordinal) { "vim", "vi", "nvim", "nano", "micro", "helix", "hx", "kak", "ne", "joe" };

    // Emacs is a window unless one of these asks for the terminal.
    private static readonly HashSet<string> _emacs = new(StringComparer.Ordinal) { "emacs", "emacsclient" };
    private static readonly HashSet<string> _tty = new(StringComparer.Ordinal) { "-nw", "-t", "--tty", "--no-window-system" };

    /// <summary>True when the command runs a terminal editor (#3836): its program's file name, with or without a path, is a known one, or Emacs with a terminal flag.</summary>
    /// <param name="argv">The command, as <see cref="Resolve"/> returns it.</param>
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

        return _terminal.Contains(name) || (_emacs.Contains(name) && argv.Skip(1).Any(_tty.Contains));
    }

    /// <summary>The command's argv without the file.</summary>
    /// <param name="environment">The three settings.</param>
    /// <returns>The program and its arguments, or null when none is set.</returns>
    internal static IReadOnlyList<string>? Resolve(EditorEnvironment environment)
    {
        foreach (var value in (string?[])[environment.Configured, environment.Visual, environment.Editor])
        {
            var argv = value?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (argv is { Length: > 0 })
            {
                return argv;
            }
        }

        return null;
    }
}
