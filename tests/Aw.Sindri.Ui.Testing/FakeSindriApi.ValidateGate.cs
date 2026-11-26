namespace Aw.Sindri.Ui.Testing;

/// <summary>The fake's hold on POST /sindri/workflows/validate (<see cref="FakeSindriApi"/>, continued): lets a test answer one call late.</summary>
internal sealed partial class FakeSindriApi
{
    /// <summary>Holds the NEXT validate call (and only it) until a test releases it; the call is recorded first.</summary>
    public TaskCompletionSource? ValidateWorkflowGate { get; set; }

    private async Task HoldValidate(CancellationToken cancellationToken)
    {
        var gate = ValidateWorkflowGate;
        ValidateWorkflowGate = null;
        await WaitFor(gate, cancellationToken).ConfigureAwait(false);
    }
}
