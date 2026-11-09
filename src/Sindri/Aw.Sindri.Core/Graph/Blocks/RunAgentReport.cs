using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The check a 'run_agent' node's report goes through (PR 7): every output the node declares must be there, in its declared type, and nothing else. A string output is the file's text;
/// any other type is a JSON value. A refusal names the field, so the agent can fix it and report again.
/// </summary>
public static class RunAgentReport
{
    /// <summary>Checks a report against the declared outputs.</summary>
    /// <param name="declared">The declared output fields with their types ('string', 'number', 'boolean', 'object', 'array'; a trailing '?' is optional).</param>
    /// <param name="reported">What the agent reported: each output's content by field name.</param>
    /// <returns>The values by field; SND-INPUT naming the first missing, undeclared or mistyped field.</returns>
    public static Result<IReadOnlyDictionary<string, JsonNode?>> Check(IReadOnlyDictionary<string, string> declared, IReadOnlyDictionary<string, ReadOnlyMemory<byte>> reported) =>
        throw new NotSupportedException("red");
}
