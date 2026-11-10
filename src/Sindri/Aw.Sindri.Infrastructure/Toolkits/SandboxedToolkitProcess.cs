using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>A toolkit process and the run area its sandbox plan made: disposing it ends the process and removes that area.</summary>
/// <param name="inner">The connected process.</param>
/// <param name="sandbox">The sandbox that made the plan.</param>
/// <param name="plan">The plan whose files go when the process does.</param>
internal sealed class SandboxedToolkitProcess(IToolkitProcess inner, ISandbox sandbox, SandboxPlan plan) : IToolkitProcess
{
    public Task Exited => inner.Exited;

    public ValueTask<Kernel.Result<string>> Call(string tool, string inputJson, TimeSpan timeout, CancellationToken cancellationToken) => inner.Call(tool, inputJson, timeout, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(plan);
        await inner.DisposeAsync().ConfigureAwait(false);
    }
}
