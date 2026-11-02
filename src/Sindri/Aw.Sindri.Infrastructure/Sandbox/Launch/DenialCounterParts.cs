namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>What a launch's <see cref="DenialCounter"/> is made from.</summary>
/// <param name="Sink">Where its 'sandbox.denied' events go.</param>
/// <param name="Json">The package's JSON options.</param>
/// <param name="Scrubber">Takes secrets out of a target.</param>
/// <param name="Clock">The time.</param>
internal sealed record DenialCounterParts(Core.Sandbox.Denials.ISandboxDenials Sink, System.Text.Json.JsonSerializerOptions Json, Core.Text.ITextScrubber Scrubber, TimeProvider Clock);
