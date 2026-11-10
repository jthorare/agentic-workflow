using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Tests.Toolkits;

/// <summary>A toolkit runtime with fixed toolkits and scripted tool answers; remembers each call.</summary>
internal sealed class FakeRunningToolkits(IReadOnlyList<Toolkit> toolkits, Func<string, string, string, Result<string>>? answer = null) : IRunningToolkits
{
    public List<(string Toolkit, string Tool, string Arguments, string Caller)> Calls { get; } = [];

    public IReadOnlyList<Toolkit> Toolkits() => toolkits;

    public ValueTask<Result<string>> CallTool(string toolkit, string tool, string argumentsJson, string caller, CancellationToken cancellationToken)
    {
        Calls.Add((toolkit, tool, argumentsJson, caller));
        return ValueTask.FromResult(answer?.Invoke(toolkit, tool, argumentsJson) ?? (Result<string>)SindriErrors.ToolkitToolUnknown.With("no scripted answer"));
    }

    public static Toolkit Make(string name, ToolkitState state, params (string Tool, string Input)[] tools)
    {
        var manifest = new ToolkitManifest(name, "1.0.0", [], [.. tools.Select(static t => new ToolkitTool(t.Tool, $"Does {t.Tool}.", t.Input, """{"type":"object"}"""))]);
        return new Toolkit(name, $"/toolkits/{name}", state, "hash", DateTimeOffset.UnixEpoch, manifest, "owner");
    }
}
