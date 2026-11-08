using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The skill card of the shown state (skill flows spec §5.2): the block from GET /sindri/skills, the waiting row from GET /sindri/needs-you, read again on every event with the instance.</summary>
internal sealed partial class InstanceFrameViewModel
{
    private SkillsView? _skills;
    private IReadOnlyList<NeedsYouRowView> _needsRows = [];

    private async Task ReadSkillCard(InstanceDetail detail, int generation, CancellationToken cancellationToken)
    {
        if (Expanded is not { } expanded || _url is not { } url)
        {
            return;
        }

        var cell = detail.States.FirstOrDefault(s => string.Equals(s.State, detail.Current.State, StringComparison.Ordinal));
        if (cell is null || !SkillCardViewModel.IsSkillState(cell))
        {
            expanded.ShowSkillCard(input: null);
            return;
        }

        var rows = await ReadSkillRows(url, cancellationToken).ConfigureAwait(true);
        if (generation == _generation)
        {
            var skills = _skills ?? new SkillsView([]);   // the list failed to read: the card still draws (no sentence) and the next read tries again
            var run = detail.StepRuns?.LastOrDefault(r => string.Equals(r.Step, cell.State, StringComparison.Ordinal));
            expanded.ShowSkillCard(new SkillCardInput(skills, detail.Address, NameOf(cell.State), cell, run, rows, detail.Facts, Cost: null, _launcher, clipboard, clock));
        }
    }

    // The block list is read until it succeeds once (it changes only with a restart); the needs-you rows on every read, so a waiting row shows and clears with the events.
    private async Task<IReadOnlyList<NeedsYouRowView>> ReadSkillRows(Uri url, CancellationToken cancellationToken)
    {
        if (_skills is null && await api.Skills(url, cancellationToken).ConfigureAwait(true) is { IsSuccess: true, Value: var skills })
        {
            _skills = skills;
        }

        var needsYou = await api.NeedsYou(url, cancellationToken).ConfigureAwait(true);
        if (needsYou.IsSuccess)
        {
            _needsRows = needsYou.Value.Rows;
        }

        return _needsRows;   // a failed read keeps the last rows, so a waiting card does not flip to running and back
    }
}
