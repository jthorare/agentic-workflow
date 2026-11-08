using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The Agents page's rows and groups (<see cref="AgentsPageViewModel"/>, continued): each is kept by its ref or name across reads, so a control with focus keeps it.</summary>
internal sealed partial class AgentsPageViewModel
{
    private SessionGroupViewModel Group(string name, List<SessionRowViewModel> rows)
    {
        if (!_groups.TryGetValue(name, out var group))
        {
            group = new SessionGroupViewModel(name);
            _groups[name] = group;
        }

        group.Show(rows);
        return group;
    }

    private SessionRowViewModel Row(SessionRowView served)
    {
        if (_rows.TryGetValue(served.Ref, out var kept))
        {
            kept.Update(served);
            return kept;
        }

        var row = new SessionRowViewModel(served, catalog.Row("FocusSession"), Clipboard, _clock);
        row.SetCompact(IsCompact);   // a row that arrives after the page narrowed is narrow too
        _rows[served.Ref] = row;
        return row;
    }

    private void ShowEnded(SessionList list)
    {
        var shown = list.RecentlyEnded.Where(e => Matches(e.Session)).Select(e =>
        {
            if (_ended.TryGetValue(e.Session.Ref, out var kept))
            {
                kept.Update(e);
                return kept;
            }

            return _ended[e.Session.Ref] = new EndedSessionViewModel(e, Clipboard, _clock);
        }).ToList();
        foreach (var gone in _ended.Keys.Where(k => shown.TrueForAll(s => !string.Equals(s.Ref, k, StringComparison.Ordinal))).ToList())
        {
            _ = _ended.Remove(gone);
        }

        Patch.To(Ended, shown);
        OnChanged(nameof(EndedHeader));
        OnChanged(nameof(HasEnded));
        OnChanged(nameof(EndedToggleName));
    }
}
