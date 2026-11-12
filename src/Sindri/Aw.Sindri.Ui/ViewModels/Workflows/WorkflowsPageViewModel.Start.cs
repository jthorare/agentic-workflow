#pragma warning disable CA1822, MA0041, IDE0060 // red commit: stubs, replaced by the green commit

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the start screen of New workflow (spec 4.1), read from GET /sindri/workflows/templates.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>The start screen: the cards from the templates route, else the app's own A, B and C.</summary>
    public StartScreenViewModel Start { get; private set => Set(ref field, value); } = new();

    /// <summary>True when the last read of the templates failed (not "not built yet"): the cards stay, with the failed line over them.</summary>
    public bool TemplatesFailed { get; private set => Set(ref field, value); }

    /// <summary>The failed line.</summary>
    public string TemplatesFailedText => string.Empty;

    /// <summary>The read of the templates, started when the form opens.</summary>
    internal Task TemplatesRead { get; private set; } = Task.CompletedTask;

    /// <summary>Reads the templates again (the failed line's Retry).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the cards are shown.</returns>
    internal Task RetryTemplates(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Picks a card: its template is the one Create sends.</summary>
    /// <param name="entry">The card.</param>
    internal void ChooseStart(StartEntry entry) => _ = entry;
}
