using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The review page's strip and panels (<see cref="ReviewPageViewModel"/>, continued; review page spec §6.2, §6.3).</summary>
internal sealed partial class ReviewPageViewModel
{
    private void ShowPanels(ReviewPage page)
    {
        var strip = page.Strip.ToDictionary(s => s.Panel, s => s.Text, StringComparer.Ordinal);
        Patch(Panels, [.. page.Panels.Select(p => Panel(p, strip.GetValueOrDefault(p.Id, string.Empty)))]);
        Patch(Strip, [.. page.Strip.Select(s => new ReviewStripSegmentViewModel(s.Panel, s.Text, StripGlyph(page.Panels.FirstOrDefault(p => string.Equals(p.Id, s.Panel, StringComparison.Ordinal))), isLoading: false))]);
    }

    private ReviewPanelViewModel Panel(ReviewPanelView served, string summary)
    {
        var state = served.State switch
        {
            "empty" => ReviewPanelState.Empty,
            "error" => ReviewPanelState.Error,
            _ => ReviewPanelState.Ready,
        };
        var collapsed = _memory.Chosen(_settings, served.Id) ?? !(IsWide || string.Equals(served.Id, "requiredChecks", StringComparison.Ordinal));
        var data = served.Data;
        return new ReviewPanelViewModel(served.Id, state, served.Text ?? string.Empty, summary, collapsed, (panel, value) => _memory.Choose(_settings, panel.Id, value))
        {
            Checks = [.. (data.Checks ?? []).Select(c => new ReviewCheckRowViewModel(c))],
            DocItems = [.. (data.Doc?.Items ?? []).Select(i => new ReviewDocItemViewModel(i))],
            ClaimsText = ClaimsOf(data.Claims),
            Lines = Lines(served.Id, data),
        };
    }

    // "Covers Ph5, REQ-E3": the ids after the doc's name in each claimed ref.
    private static string ClaimsOf(IReadOnlyList<ReviewClaimView>? claims) =>
        claims is { Count: > 0 } ? "Covers " + string.Join(", ", claims.Select(c => c.Ref[(c.Ref.IndexOf('#', StringComparison.Ordinal) + 1)..])) : string.Empty;

    private static StateGlyph StripGlyph(ReviewPanelView? panel) => panel switch
    {
        null => StateGlyph.Ok,
        { State: "error" } => StateGlyph.Down,
        { Id: "requiredChecks", Data.Checks: { } checks } => checks.Any(c => string.Equals(c.Status, "fail", StringComparison.Ordinal)) ? StateGlyph.Down : checks.Any(c => !string.Equals(c.Status, "pass", StringComparison.Ordinal)) ? StateGlyph.Warning : StateGlyph.Ok,
        { Id: "risk", Data.Hits.Count: > 0 } => StateGlyph.Warning,
        { Id: "ci", Data.Runs: { } runs } => runs.Any(r => string.Equals(r.Conclusion, "failure", StringComparison.Ordinal)) ? StateGlyph.Down : runs.Any(r => !string.Equals(r.Status, "completed", StringComparison.Ordinal)) ? StateGlyph.Stale : StateGlyph.Ok,
        _ => StateGlyph.Ok,
    };

    private static List<ReviewPanelLine> Lines(string id, ReviewPanelData data) => id switch
    {
        "testPlan" => [.. (data.Rows ?? []).Select(r => new ReviewPanelLine(r.Text, $"{r.Id}, verifies {string.Join(", ", r.Verifies)}", Url: null, r.Ticked ? StateGlyph.Ok : StateGlyph.None))],
        "ci" => [.. (data.Runs ?? []).Select(r => new ReviewPanelLine(r.Name, $"{r.Conclusion ?? r.Status} on {r.Sha7}", r.Url, RunGlyph(r)))],
        "evidence" => [.. (data.Items ?? []).Select(i => new ReviewPanelLine(i.Label, i.Kind, i.Url, StateGlyph.Ok))],
        "risk" => [.. (data.Hits ?? []).Select(h => new ReviewPanelLine(h.Path, h.Why, Url: null, StateGlyph.Warning))],
        "conversation" => [.. (data.Threads ?? []).Select(ThreadLine)],
        _ => [],
    };

    private static ReviewPanelLine ThreadLine(ReviewThreadView thread)
    {
        var where = thread.Line is { } line ? $"{thread.Path}:{line.ToString(System.Globalization.CultureInfo.InvariantCulture)}" : thread.Path;
        var first = thread.Comments.Count > 0 ? $"{thread.Comments[0].Author}: {thread.Comments[0].Body}" : string.Empty;
        return new ReviewPanelLine(where, first, Url: null, thread.Resolved ? StateGlyph.Ok : StateGlyph.None);
    }

    private static StateGlyph RunGlyph(ReviewRunView run) =>
        !string.Equals(run.Status, "completed", StringComparison.Ordinal) ? StateGlyph.Stale : string.Equals(run.Conclusion, "success", StringComparison.Ordinal) ? StateGlyph.Ok : StateGlyph.Down;
}
