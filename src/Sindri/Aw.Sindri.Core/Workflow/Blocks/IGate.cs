namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>A gate block (spec §2): checks a transition. Registered with AddGate.</summary>
public interface IGate
{
    /// <summary>Evaluates the gate.</summary>
    /// <param name="context">What it evaluates.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>Its verdict.</returns>
    public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken);
}
