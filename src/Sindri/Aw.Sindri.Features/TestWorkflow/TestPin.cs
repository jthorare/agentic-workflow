namespace Aw.Sindri.Features.TestWorkflow;

/// <summary>One step output a test run uses in place of calling the step.</summary>
/// <param name="Node">The step's node id.</param>
/// <param name="Output">The output, as JSON text.</param>
internal sealed record TestPin(string Node, string Output);
