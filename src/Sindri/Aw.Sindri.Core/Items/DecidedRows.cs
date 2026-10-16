using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>The rows after a decision is recorded.</summary>
/// <param name="Request">The request, accepted or declined.</param>
/// <param name="Decision">The decision, decided.</param>
internal sealed record DecidedRows(RequestRow Request, HumanDecisionRow Decision);
