#pragma warning disable CA1822, MA0041, IDE0060 // red commit: stubs, replaced by the green commit

using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): what POST /sindri/workflows/validate found on the open workflow's newest revision, and the header's one primary button (spec 4.5, 4.7).</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>The findings of the last check, errors and warnings.</summary>
    public IReadOnlyList<WorkflowFindingView> Findings { get; private set => Set(ref field, value); } = [];

    /// <summary>"2 things to fix"; empty when no finding is an error.</summary>
    public string FindingsText => string.Empty;

    /// <summary>True when the findings chip shows.</summary>
    public bool HasFindings => false;

    /// <summary>True while the checks are not built yet (SND-GRAPH-113): the page shows no chip and no error.</summary>
    public bool ValidationNotBuilt { get; private set => Set(ref field, value); }

    /// <summary>The header's primary button: the one next step.</summary>
    public string PrimaryText => string.Empty;

    /// <summary>The check started when the newest revision changed.</summary>
    internal Task Validating { get; private set; } = Task.CompletedTask;
}
