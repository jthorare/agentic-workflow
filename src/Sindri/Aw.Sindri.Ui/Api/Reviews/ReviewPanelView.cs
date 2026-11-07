namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One panel: its id, state (ready, empty or error), the words of an empty or error state, and its data.</summary>
/// <param name="Id">requiredChecks, docMap, testPlan, ci, evidence, risk or conversation.</param>
/// <param name="State">ready, empty or error.</param>
/// <param name="Text">The empty or error words; null when ready.</param>
/// <param name="Data">The panel's rows; the field that matches its id is the one that counts.</param>
internal sealed record ReviewPanelView(string Id, string State, string? Text, ReviewPanelData Data);
