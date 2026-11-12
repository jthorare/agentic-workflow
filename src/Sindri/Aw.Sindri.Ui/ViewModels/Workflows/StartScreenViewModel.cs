using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The start screen of New workflow (spec 4.1): templates A, B and C, then Blank. "Describe it" is not offered before P6.</summary>
internal sealed class StartScreenViewModel : ObservableObject
{
    private const string Never = "never on this box";

    private static readonly StartEntry _blank = new(string.Empty, string.Empty, "Blank", "An empty trigger slot. You start at Add a trigger.", string.Empty, string.Empty, string.Empty, string.Empty);

    // The template names are the ones the templates route will serve (P1e); until it lands they are the app's guess and the gallery's fallback.
    private static readonly StartEntry[] _fallback =
    [
        new("A", "review-every-pr", "Review every PR", "An agent reviews each pull request and you get a notification.", "PR opened", "3 \u00b7 trigger, agent, notification", "GitHub connection", Never),
        new("B", "plan-labelled-issue", "Plan a labelled issue", "A model writes a plan when an issue gets a label, and comments it.", "Issue labelled", "3 \u00b7 trigger, prompt, comment", "GitHub connection", Never),
        new("C", "watch-main", "Watch main, open an issue on red", "Every weekday a check runs. If it is red, an issue opens.", "Schedule, weekdays 7:00", "4 \u00b7 schedule, check, if, issue", "nothing", Never),
        _blank,
    ];

    /// <summary>Makes the screen from the app's own cards: A, B, C, then Blank.</summary>
    internal StartScreenViewModel()
    {
        Entries = _fallback;
    }

    /// <summary>Makes the screen from the templates the route served, then Blank.</summary>
    /// <param name="templates">The templates, in the order served.</param>
    /// <param name="clock">The clock "last ran" is told against.</param>
    internal StartScreenViewModel(WorkflowTemplateList templates, TimeProvider clock)
    {
        Entries = [.. templates.Templates.Select((t, i) => Card(t, i, clock)), _blank];
    }

    /// <summary>The entries: the templates, then Blank.</summary>
    public IReadOnlyList<StartEntry> Entries { get; }

    /// <summary>Gets the index of the selected entry (A at first).</summary>
    public int SelectedIndex { get; private set; }

    /// <summary>Gets the selected entry.</summary>
    public StartEntry Selected => Entries[SelectedIndex];

    /// <summary>Moves the selection; it stays on the screen's edge.</summary>
    /// <param name="move">The direction.</param>
    public void Move(StartMove move)
    {
        var blank = Entries.Count - 1;
        Select(move switch
        {
            StartMove.Left => Math.Max(0, SelectedIndex - 1),
            StartMove.Right => Math.Min(blank, SelectedIndex + 1),
            StartMove.Down => SelectedIndex < blank ? blank : SelectedIndex,
            StartMove.Up => SelectedIndex == blank ? 0 : SelectedIndex,
            _ => SelectedIndex,
        });
    }

    /// <summary>Selects an entry by index; out of range does nothing.</summary>
    /// <param name="index">The index.</param>
    public void Select(int index)
    {
        if (index < 0 || index >= Entries.Count || index == SelectedIndex)
        {
            return;
        }

        SelectedIndex = index;
        OnChanged(nameof(SelectedIndex));
        OnChanged(nameof(Selected));
    }

    /// <summary>Chooses the selected entry (Enter).</summary>
    /// <returns>The entry chosen.</returns>
    public StartEntry Choose() => Selected;

    // Not every template carries a description or step kinds: the card says what the route serves, in words.
    private static StartEntry Card(WorkflowTemplateView template, int index, TimeProvider clock) => new(
        template.Id.Length == 1 ? template.Id.ToUpperInvariant() : ((char)('A' + index)).ToString(),
        template.Id,
        template.Title,
        string.Empty,
        TriggerWords(template.Trigger),
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{template.Steps} {(template.Steps == 1 ? "step" : "steps")}"),
        NeedsWords(template.Needs),
        template.LastRunAt is { } ran ? "last ran " + Labels.AgoWords(clock.GetUtcNow() - ran) : Never);

    private static string TriggerWords(string trigger) => trigger switch
    {
        "manual" => "Manual",
        "schedule" => "Schedule",
        _ => trigger,
    };

    private static string NeedsWords(IReadOnlyList<string> needs) =>
        needs.Count == 0 ? "nothing" : string.Join(", ", needs.Select(Need));

    // "connection:github" -> "GitHub connection"; any other need is shown as served.
    private static string Need(string need)
    {
        const string connection = "connection:";
        if (!need.StartsWith(connection, StringComparison.Ordinal))
        {
            return need;
        }

        var service = need[connection.Length..];
        return (string.Equals(service, "github", StringComparison.OrdinalIgnoreCase) ? "GitHub" : service.Length == 0 ? service : char.ToUpperInvariant(service[0]) + service[1..]) + " connection";
    }
}
