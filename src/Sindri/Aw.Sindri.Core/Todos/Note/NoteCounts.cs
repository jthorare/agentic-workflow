namespace Aw.Sindri.Core.Todos.Note;

/// <summary>What a note holds, for the Settings card's Check note (todo spec §3.4).</summary>
/// <param name="Open">Open task lines ('- [ ]'), those that can't import included.</param>
/// <param name="Done">Done task lines ('- [x]').</param>
/// <param name="Other">Every line that isn't a top-level task line.</param>
/// <param name="Importable">Open task lines that are valid todos (D3), the ones an import would adopt.</param>
/// <param name="CantImport">Every open task line that breaks D3, such as a markdown link or a due marker inside the text, in file order.</param>
public sealed record NoteCounts(int Open, int Done, int Other, int Importable, IReadOnlyList<NoteCantImport> CantImport);
