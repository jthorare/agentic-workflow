namespace Aw.Sindri.Features.SetupWatch;

/// <summary>What the first-run steps remember between ticks. It is the only singleton of them: the steps are scoped, one per tick, and a start that failed must be tried again on a later tick.</summary>
internal sealed class HelloRunState
{
    /// <summary>Gets or sets a value indicating whether nothing more is to be tried: the run started, was refused as done, hello-sindri is gone, or it is not ours.</summary>
    internal bool Settled { get; set; }
}
