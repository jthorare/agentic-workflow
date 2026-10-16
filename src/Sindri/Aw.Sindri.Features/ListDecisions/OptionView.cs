using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.ListDecisions;

/// <summary>An option of a decision (U3-API-3, -6).</summary>
/// <param name="Id">The option, what 'Decide' takes as its choice.</param>
/// <param name="Label">What the button says.</param>
/// <param name="RejectLike">True for the option the X key picks (Decline).</param>
/// <param name="Cli">The command line that picks it, built through CliForm.Fill.</param>
public sealed record OptionView(DecisionOption Id, string Label, bool RejectLike, string Cli);
