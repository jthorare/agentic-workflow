namespace Aw.Judge.Core.Questions;

public static partial class UntrustedText
{
    /// <summary>
    /// A set of files cut to <paramref name="cap"/> characters of text by whole files, in the order <see cref="CappedDiff"/> uses
    /// (files under src/ first, then the rest, each in the order given): a file is kept only if it still fits, and when none fits
    /// the first in that order is cut at a line instead of dropped. Whatever was dropped or cut is named with its size.
    /// </summary>
    /// <param name="files">The files, with their text.</param>
    /// <param name="cap">How many characters of text to keep.</param>
    /// <returns>The kept files (in the order given), the dropped ones and the cut one.</returns>
    public static CappedFileSet CappedFiles(IReadOnlyList<ModelPickFile> files, int cap)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Sum(static f => f.Text.Length) <= cap)
        {
            return new CappedFileSet(files, [], []);
        }

        var sections = files.Select(static f => (f.Path, f.Text)).ToList();
        var order = Enumerable.Range(0, files.Count).OrderBy(i => IsSource(files[i].Path) ? 0 : 1).ThenBy(static i => i).ToList();
        var kept = cap > 0 ? Fitting(sections, order, cap) : [];
        int? cutIndex = cap > 0 && kept.Count == 0 ? order[0] : null;
        List<ModelPickFile> stayed = [];
        List<ModelPickOmitted> omitted = [];
        List<ModelPickCut> cut = [];
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (i == cutIndex)
            {
                var text = CutAtLine(file.Text, cap);
                stayed.Add(file with { Text = text });
                cut.Add(new ModelPickCut(file.Path, text.Length, file.Text.Length));
            }
            else if (kept.Contains(i))
            {
                stayed.Add(file);
            }
            else
            {
                omitted.Add(new ModelPickOmitted(file.Path, file.Text.Length, "cap"));
            }
        }

        return new CappedFileSet(stayed, omitted, cut);
    }
}
