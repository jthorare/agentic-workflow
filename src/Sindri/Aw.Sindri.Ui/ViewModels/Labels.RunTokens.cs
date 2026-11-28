using System.Globalization;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels;

internal static partial class Labels
{
    /// <summary>A run's tokens against its budget (#3805): "1,200 / 5,000 tokens", "1,200 tokens" with no budget, and nothing for a run that used none and has no budget (or a Sindri that doesn't serve them).</summary>
    /// <param name="tokens">The run's tokens as served, or null.</param>
    /// <returns>The words, or empty.</returns>
    internal static string WfRunTokens(WorkflowRunTokens? tokens) => tokens switch
    {
        null or { Used: <= 0, Budget: null } => string.Empty,
        { Budget: { } budget } capped => string.Create(CultureInfo.InvariantCulture, $"{capped.Used:N0} / {budget:N0} tokens"),
        { } open => string.Create(CultureInfo.InvariantCulture, $"{open.Used:N0} tokens"),
    };
}
