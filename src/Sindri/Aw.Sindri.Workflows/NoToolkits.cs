using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The toolkit runtime of a build that registers none: no toolkit exists, so 'call_tool' reaches connections only and a toolkit tool is SND-TK-001.</summary>
internal sealed class NoToolkits : IRunningToolkits
{
    public static NoToolkits Instance { get; } = new();

    public IReadOnlyList<Toolkit> Toolkits() => [];

    public ValueTask<Result<string>> CallTool(string toolkit, string tool, string argumentsJson, string caller, CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<string>>(SindriErrors.ToolkitNotFound.With($"No toolkit named '{toolkit}'"));
}
