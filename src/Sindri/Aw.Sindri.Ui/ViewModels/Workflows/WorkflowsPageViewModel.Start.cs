using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the start screen of New workflow (spec 4.1), read from GET /sindri/workflows/templates.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>The code a new route answers until the engine builds it (GraphNotBuilt): the page shows "not built yet", never an error.</summary>
    internal const string GraphNotBuilt = "SND-GRAPH-119";
    private const string BlankTemplate = "blank";

    private int _templateReads;

    /// <summary>The start screen: the cards from the templates route, else the app's own A, B and C.</summary>
    public StartScreenViewModel Start { get; private set => Set(ref field, value); } = new();

    /// <summary>True when the last read of the templates failed (not "not built yet"): the cards stay, with the failed line over them.</summary>
    public bool TemplatesFailed { get; private set { Set(ref field, value); OnChanged(nameof(TemplatesFailedText)); } }

    /// <summary>The failed line, "Couldn't load templates. Retry"; empty while the read has not failed.</summary>
    public string TemplatesFailedText => TemplatesFailed ? EditorStates.TemplatesFailed : string.Empty;

    /// <summary>The read of the templates, started when the form opens.</summary>
    internal Task TemplatesRead { get; private set; } = Task.CompletedTask;

    /// <summary>Reads the templates again (the failed line's Retry).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the cards are shown.</returns>
    internal Task RetryTemplates(CancellationToken cancellationToken) => _url is { } url ? ReadTemplates(url, cancellationToken) : Task.CompletedTask;

    /// <summary>Picks a card: its template is the one Create sends.</summary>
    /// <param name="entry">The card.</param>
    internal void ChooseStart(StartEntry entry)
    {
        var name = entry.IsBlank ? BlankTemplate : entry.Name;
        if (Templates.All(t => !string.Equals(t.Name, name, StringComparison.Ordinal)))
        {
            Templates.Add(new TemplateChoice(name, entry.Title, entry.Description));
        }

        SelectedTemplate = name;
        Start.Select(IndexOf(entry));
    }

    private int IndexOf(StartEntry entry)
    {
        for (var i = 0; i < Start.Entries.Count; i++)
        {
            if (ReferenceEquals(Start.Entries[i], entry))
            {
                return i;
            }
        }

        return -1;
    }

    private async Task ReadTemplates(Uri url, CancellationToken cancellationToken)
    {
        var read = ++_templateReads;
        var answer = await Api.WorkflowTemplates(url, cancellationToken).ConfigureAwait(true);
        if (read != _templateReads)
        {
            return;   // a newer read was asked for since
        }

        if (answer.IsSuccess)
        {
            Start = new StartScreenViewModel(answer.Value, _clock);
            TemplatesFailed = false;
            OfferTemplates(answer.Value);
            return;
        }

        // Not built yet is not a failure: the app's own cards stay and nothing is said. Any other answer keeps them with the failed line.
        Start = new StartScreenViewModel();
        TemplatesFailed = !string.Equals(answer.Error.Code, GraphNotBuilt, StringComparison.Ordinal);
    }

    // The picker holds what the route serves, then Blank; the pick stays when the route still has it.
    private void OfferTemplates(WorkflowTemplateList list)
    {
        var choices = list.Templates.Select(t => new TemplateChoice(t.Id, t.Title, string.Empty)).Append(new TemplateChoice(BlankTemplate, "Blank", "An empty trigger slot.")).ToList();
        Patch.To(Templates, choices);
        if (Templates.All(t => !string.Equals(t.Name, SelectedTemplate, StringComparison.Ordinal)) && Templates.Count > 0)
        {
            SelectedTemplate = Templates[0].Name;
        }
    }
}
