namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The editors' actions (<see cref="ReviewSettingsViewModel"/>, continued): move a panel, add and remove rows, reset a key. Rows are rebuilt on a change so their screen-reader numbers stay right.</summary>
internal sealed partial class ReviewSettingsViewModel
{
    /// <summary>Moves a panel one place up or down.</summary>
    /// <param name="row">The panel.</param>
    /// <param name="by">-1 for up, 1 for down.</param>
    internal void MovePanel(SettingsPanelRow row, int by)
    {
        var rows = PanelRows.ToList();
        var from = rows.IndexOf(row);
        var to = from + by;
        if (!CanEdit || from < 0 || to < 0 || to >= rows.Count)
        {
            return;
        }

        rows.RemoveAt(from);
        rows.Insert(to, row);
        PanelRows = rows;
    }

    /// <summary>Adds an empty group at the end.</summary>
    internal void AddGroup()
    {
        if (CanEdit)
        {
            GroupRows = [.. GroupRows, new SettingsGroupRow(GroupRows.Count + 1, string.Empty, string.Empty)];
        }
    }

    /// <summary>Removes a group.</summary>
    /// <param name="row">The group.</param>
    internal void RemoveGroup(SettingsGroupRow row)
    {
        if (CanEdit)
        {
            GroupRows = [.. GroupRows.Where(g => g != row).Select((g, i) => g.At(i + 1))];
        }
    }

    /// <summary>Adds an empty risky path at the end.</summary>
    internal void AddRisk()
    {
        if (CanEdit)
        {
            RiskRows = [.. RiskRows, new SettingsRiskRow(RiskRows.Count + 1, string.Empty, string.Empty)];
        }
    }

    /// <summary>Removes a risky path.</summary>
    /// <param name="row">The risky path.</param>
    internal void RemoveRisk(SettingsRiskRow row)
    {
        if (CanEdit)
        {
            RiskRows = [.. RiskRows.Where(r => r != row).Select((r, i) => r.At(i + 1))];
        }
    }

    /// <summary>Adds an empty check at the end.</summary>
    internal void AddCheck()
    {
        if (CanEdit)
        {
            CheckRows = [.. CheckRows, new SettingsCheckRow(CheckRows.Count + 1, string.Empty, string.Empty, string.Empty, string.Empty, need: null)];
        }
    }

    /// <summary>Removes a check.</summary>
    /// <param name="row">The check.</param>
    internal void RemoveCheck(SettingsCheckRow row)
    {
        if (CanEdit)
        {
            CheckRows = [.. CheckRows.Where(c => c != row).Select((c, i) => c.At(i + 1))];
        }
    }

    /// <summary>Puts a key back on what it inherits: an entry that sets it stops setting it on save, an inherited key goes back to the value that was read.</summary>
    /// <param name="key">The key.</param>
    internal void Reset(ReviewSettingsKey key)
    {
        if (!CanEdit || _read is not { } read)
        {
            return;
        }

        key.Error = string.Empty;
        if (key.WasOwn)
        {
            key.IsReset = true;
            return;
        }

        var resolved = read.Resolved;
        switch (key.Id)
        {
            case PanelsKeyId:
                PanelRows = [.. resolved.Panels.Concat(read.AllPanels.Except(resolved.Panels, StringComparer.Ordinal)).Select(id => new SettingsPanelRow(id, resolved.Panels.Contains(id, StringComparer.Ordinal)))];
                break;
            case GroupsKeyId:
                GroupRows = [.. resolved.Groups.Select((g, i) => new SettingsGroupRow(i + 1, g.Name, Join(g.Globs)))];
                break;
            case CollapseKeyId:
                CollapseText = Join(resolved.Collapse);
                break;
            case RiskyKeyId:
                RiskRows = [.. resolved.Risky.Select((r, i) => new SettingsRiskRow(i + 1, Join(r.Globs), r.Why))];
                break;
            default:
                CheckRows = [.. resolved.Checks.Select((c, i) => new SettingsCheckRow(i + 1, c.Id, c.Title, Join(c.When ?? []), ReviewSettingsNeed.Text(c.Need), c.Need))];
                break;
        }
    }
}
