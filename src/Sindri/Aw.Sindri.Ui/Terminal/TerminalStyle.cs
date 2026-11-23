namespace Aw.Sindri.Ui.Terminal;

/// <summary>The text attributes of one cell.</summary>
[Flags]
internal enum TerminalStyle
{
    /// <summary>No attribute.</summary>
    None = 0,

    /// <summary>SGR 1.</summary>
    Bold = 1,

    /// <summary>SGR 3.</summary>
    Italic = 2,

    /// <summary>SGR 4.</summary>
    Underline = 4,

    /// <summary>SGR 7: foreground and background swap.</summary>
    Inverse = 8,

    /// <summary>SGR 2: drawn at lower opacity (Claude's placeholder text).</summary>
    Dim = 16,
}
