namespace Aw.Judge.Features.PromoteScaffolds;

/// <summary>The PromoteScaffolds request ('judge prompt-sort promote [--apply]').</summary>
/// <param name="Apply">Write each go verdict into config.json (--apply).</param>
public sealed record PromoteScaffoldsRequest(bool Apply = false);
