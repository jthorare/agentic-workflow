using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The question first (<see cref="ContextPageViewModel"/>, continued): the files the engine returned for the last question, shown as a list (#2974).</summary>
internal sealed partial class ContextPageViewModel
{
    private const string CodePrefix = "code:";
    private const int MaxAnswerFiles = 25;   // the rest are counted in words under the list

    private int _answerFilesHidden;

    private IReadOnlyList<AnswerFile> _askedFiles = [];

    /// <summary>The files the last resolved question returned, in the order the answer gave them.</summary>
    public IReadOnlyList<AnswerFile> AnswerFiles { get; private set; } = [];

    /// <summary>Whether the list has files to show.</summary>
    public bool HasAnswerFiles => AnswerFiles.Count > 0;

    /// <summary>How many more files the answer named than the list shows, in words; empty when it shows them all.</summary>
    public string AnswerFilesMore => _answerFilesHidden > 0 ? Labels.ContextAnswerFilesMore(_answerFilesHidden) : string.Empty;

    /// <summary>Whether some of the answer's files are left off the list.</summary>
    public bool HasAnswerFilesMore => _answerFilesHidden > 0;

    /// <summary>The list's name.</summary>
    public string AnswerFilesName { get; } = Labels.ContextAnswerFiles;

    /// <summary>A plain sentence for a stage that draws no nodes after a question; empty when the stage draws something.</summary>
    public string StageMessage { get; private set; } = string.Empty;

    /// <summary>Whether the stage says something.</summary>
    public bool HasStageMessage => StageMessage.Length > 0;

    // The code items of the answer, each file once, in the answer's order; the reason is the engine's text, else why it named the file.
    private static List<AnswerFile> FilesOf(QueryAnchorsView answer)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return [.. (answer.Sections ?? []).SelectMany(s => s.Items ?? []).Where(i => CodePath(i).Length > 0 && seen.Add(CodePath(i))).Select(AnswerFileOf)];
    }

    private static string CodePath(QueryItemView item) =>
        string.Equals(item.Kind, "code", StringComparison.Ordinal) && item.Node is { } node && node.StartsWith(CodePrefix, StringComparison.Ordinal) ? node[CodePrefix.Length..] : string.Empty;

    private static AnswerFile AnswerFileOf(QueryItemView item)
    {
        var path = CodePath(item);
        var slash = path.LastIndexOf('/');
        var reason = string.IsNullOrWhiteSpace(item.Text) ? string.Join(", ", item.Why ?? []) : item.Text.Trim();
        return new AnswerFile(path, path[..(slash + 1)], path[(slash + 1)..], reason);
    }

    // Called after Show: a question that resolved keeps its files on the page (the first MaxAnswerFiles, the rest counted), and a lens stage with no nodes says so.
    // With the whole map ready the stage shows the whole map, not the lens, so it never draws nothing; a zoom that finds none of the answer says so itself (ContextWholeNoMatch).
    private void ShowAnswerFiles(bool fromQuestion)
    {
        var files = fromQuestion ? _askedFiles : [];
        AnswerFiles = [.. files.Take(MaxAnswerFiles)];
        _answerFilesHidden = files.Count - AnswerFiles.Count;
        var drawsNothing = !Whole.IsReady && (Map is null || Map.Nodes.Count == 0);
        StageMessage = !fromQuestion || !drawsNothing ? string.Empty
            : files.Count > 0 ? Labels.ContextStageEmptyWithFiles(files.Count)
            : Labels.ContextStageEmpty;
    }
}
