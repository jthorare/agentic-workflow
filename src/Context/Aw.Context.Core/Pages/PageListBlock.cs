namespace Aw.Context.Core.Pages;

/// <summary>One page of a pasted list: its name, then its actions.</summary>
/// <param name="Block">Its 1-based number in the paste ('Same page as block 2').</param>
/// <param name="Line">The 1-based line of its name in the paste.</param>
/// <param name="Name">The page name, bullet or number dropped.</param>
/// <param name="Actions">One per line after the name, in order.</param>
public sealed record PageListBlock(int Block, int Line, string Name, IReadOnlyList<PageListAction> Actions);
