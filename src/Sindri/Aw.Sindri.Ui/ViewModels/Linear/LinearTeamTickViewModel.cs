using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One team box of Settings' Linear card: "Engineering ENG", ticked when Sindri may write to the team.</summary>
internal sealed class LinearTeamTickViewModel : ObservableObject
{
    private readonly Action _ticked;

    /// <summary>A team box, unticked.</summary>
    /// <param name="team">The team.</param>
    /// <param name="ticked">Called when the box is ticked or unticked.</param>
    internal LinearTeamTickViewModel(LinearTeam team, Action ticked)
    {
        ArgumentNullException.ThrowIfNull(team);
        (Key, Name, _ticked) = (team.Key, team.DisplayName, ticked);
    }

    /// <summary>The team key, such as ENG.</summary>
    public string Key { get; }

    /// <summary>The team's name, else its key.</summary>
    public string Name { get; }

    /// <summary>The box's accessible name.</summary>
    public string AutomationName => Labels.LinearConnTeamName(Name, Key);

    /// <summary>True when the box is ticked.</summary>
    public bool IsTicked
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnChanged(nameof(IsTicked));
            _ticked();
        }
    }
}
