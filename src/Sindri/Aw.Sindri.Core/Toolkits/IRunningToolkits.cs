using Aw.Kernel;

namespace Aw.Sindri.Core.Toolkits;

/// <summary>
/// What the MCP endpoint and the 'call_tool' block need from the toolkit runtime: which toolkits exist (with their state and manifest) and a way to call one tool.
/// The supervisor (Infrastructure/Toolkits) implements it; tests use a fake.
/// </summary>
public interface IRunningToolkits
{
    /// <summary>Every toolkit Sindri knows with its state, in name order. Callers filter on <see cref="Toolkit.State"/>; a built, paused or off toolkit still carries its manifest when it has one.</summary>
    /// <returns>The toolkits right now.</returns>
    public IReadOnlyList<Toolkit> Toolkits();

    /// <summary>Calls one tool of a running toolkit.</summary>
    /// <param name="toolkit">The toolkit name.</param>
    /// <param name="tool">The tool name (without the toolkit prefix).</param>
    /// <param name="argumentsJson">The arguments, a JSON object as text.</param>
    /// <param name="caller">Who asked: an MCP session or a workflow run, for the call log.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tool's JSON result as text, or SND-TK-005 (paused), SND-TK-006 (timeout), SND-TK-007 (no such tool), SND-TK-004 (process ended), SND-TK-001 (not running).</returns>
    public ValueTask<Result<string>> CallTool(string toolkit, string tool, string argumentsJson, string caller, CancellationToken cancellationToken);
}
