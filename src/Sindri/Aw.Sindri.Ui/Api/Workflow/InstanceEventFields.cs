namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>
/// The payload fields the app reads. All optional: a server before engine P8 serves no via, tokens or reason, and an
/// event that is no transition serves none of them.
/// </summary>
/// <param name="From">The state before.</param>
/// <param name="To">The state after.</param>
/// <param name="Trigger">The trigger kind ('step.completed', 'step.failed', …).</param>
/// <param name="Via">'agent' when an agent session ran the Step, 'block' for the block runtime.</param>
/// <param name="Tokens">The tokens an agent run spent.</param>
/// <param name="Reason">A step.failed's reason: 'launch', 'lost', 'exit' or 'limit'.</param>
/// <param name="Limit">A step.failed whose reason is 'limit': which limit, its value, the elapsed seconds and the kept branch; null (omitted) otherwise.</param>
internal sealed record InstanceEventFields(string? From = null, string? To = null, string? Trigger = null, string? Via = null, int? Tokens = null, string? Reason = null, Sandbox.StepLimitView? Limit = null);
