using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Infrastructure.Sandbox.Launch;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>
/// Starts one toolkit as a sandboxed process: finds its binary, plans the process (<see cref="ISandbox.PlanProcess"/>: no network but the declared hosts, no resource mounts yet), wraps the argv
/// (<see cref="ISandbox.WrapProcess"/>) and connects an MCP client over its stdio. A box with no process sandbox answers SND-TK-008; the toolkit never runs unsandboxed.
/// </summary>
/// <param name="sandbox">The confinement.</param>
/// <param name="inputs">The box facts (the same ones an agent launch uses).</param>
internal sealed class ToolkitProcess(ISandbox sandbox, ISandboxLaunchInputs inputs) : IToolkitProcessLauncher
{
    public async ValueTask<Result<IToolkitProcess>> Launch(Toolkit toolkit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolkit);
        var binary = BinaryOf(toolkit);
        if (binary is null)
        {
            return SindriErrors.ToolkitNotBuilt.With($"{toolkit.Name} has no built binary under {Path.Combine(toolkit.Dir, "bin")}");
        }

        var launch = LaunchId.For(Guid.NewGuid());
        var request = new ProcessSandboxRequest(launch, "toolkit:" + toolkit.Name, binary, inputs.RunAreaFor(launch), [], new Dictionary<string, string>(StringComparer.Ordinal), [],
            new SandboxNetFacts(inputs.NetDirFor(launch), inputs.SindriPort(), inputs.ProxyPort()), inputs.Host());
        var plan = sandbox.PlanProcess(request);
        if (plan.IsError)
        {
            return SindriErrors.ToolkitSandboxUnavailable.With(plan.Error.Message);
        }

        var wrapped = sandbox.WrapProcess(plan.Value, [binary]);
        if (wrapped.IsError)
        {
            return SindriErrors.ToolkitSandboxUnavailable.With(wrapped.Error.Message);
        }

        var client = await ToolkitMcpClient.Connect(toolkit.Name, wrapped.Value, toolkit.Dir, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            sandbox.Discard(plan.Value);
            return client.Error;
        }

        return client.Value;
    }

    /// <summary>The newest file named after the toolkit under its bin folder (what `dotnet build` or a publish left), or null.</summary>
    /// <param name="toolkit">The toolkit.</param>
    /// <returns>The absolute path, or null.</returns>
    internal static string? BinaryOf(Toolkit toolkit)
    {
        var bin = Path.Combine(toolkit.Dir, "bin");
        return Directory.Exists(bin)
            ? Directory.EnumerateFiles(bin, toolkit.Name, SearchOption.AllDirectories).OrderByDescending(static f => File.GetLastWriteTimeUtc(f)).FirstOrDefault()
            : null;
    }
}
