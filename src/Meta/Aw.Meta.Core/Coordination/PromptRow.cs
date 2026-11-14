namespace Aw.Meta.Core.Coordination;

/// <summary>Finds a Claude session's input box in a pane capture.</summary>
internal static class PromptRow
{
    /// <summary>The index of the input box's prompt row. The box is framed: a rule above its prompt row and a rule below. A list under the box (background agents) has a selector row starting with the mark too, with no rule above it. With no framed row, the last row starting with the mark.</summary>
    /// <param name="lines">The pane's lines.</param>
    /// <returns>The index, or -1 when no line starts with the prompt mark.</returns>
    internal static int Find(string[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        for (var i = lines.Length - 1; i > 0; i--)
        {
            if (lines[i].StartsWith('❯') && lines[i - 1].StartsWith('─'))
            {
                return i;
            }
        }

        return Array.FindLastIndex(lines, static l => l.StartsWith('❯'));
    }
}
