namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>Why a Step ended on a limit (wire contract item 7): on the step.failed instance event whose reason is 'limit'.</summary>
/// <param name="Kind">'memory' or 'wall'.</param>
/// <param name="Limit">The limit as text: '12GiB' or '40m'.</param>
/// <param name="Elapsed">Seconds the session ran, as text.</param>
/// <param name="Branch">The run branch kept because it has commits beyond its base; null otherwise.</param>
internal sealed record StepLimitView(string Kind, string Limit, string Elapsed, string? Branch);
