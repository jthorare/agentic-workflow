using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>The rows a filed request makes.</summary>
/// <param name="Request">The request, open.</param>
/// <param name="Decision">Its decision, pending.</param>
internal sealed record FiledRows(RequestRow Request, HumanDecisionRow Decision);
