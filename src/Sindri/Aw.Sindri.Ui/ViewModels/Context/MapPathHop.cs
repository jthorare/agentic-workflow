namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One step of a path: the node reached and the type of the edge walked to reach it.</summary>
/// <param name="Key">The node reached.</param>
/// <param name="Type">The edge's served type.</param>
internal sealed record MapPathHop(string Key, string Type);
