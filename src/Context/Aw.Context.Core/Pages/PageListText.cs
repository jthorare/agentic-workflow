namespace Aw.Context.Core.Pages;

/// <summary>Reads a pasted page list into blocks: blank lines separate pages, the first line of a block is the page name, every other line an action.</summary>
public static class PageListText
{
    /// <summary>Splits the paste into blocks.</summary>
    /// <param name="text">The paste.</param>
    /// <returns>The blocks in paste order, numbered from 1.</returns>
    public static IReadOnlyList<PageListBlock> Parse(string text) => throw new NotSupportedException();
}
