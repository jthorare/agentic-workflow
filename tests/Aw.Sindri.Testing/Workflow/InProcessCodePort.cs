using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Infrastructure.Workflow.Code;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// Runs a 'code' node's body inside the test process: the real Roslyn compile (<see cref="CodeCompiler"/>), then a call to the body in a collectible load context.
/// No runner process, no sandbox, no timeout or memory cap: it tests what a shipped body returns, never the runner (CodeRunnerTests and SandboxedCodeRunnerTests do that).
/// A body must return a JsonNode, a string or null, the results the shipped bodies give.
/// </summary>
public sealed class InProcessCodePort : ICodeRunPort
{
    // why: one compile per body text per test process; Roslyn takes a few hundred ms per body even warm
    private static readonly ConcurrentDictionary<string, byte[]> _compiled = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<Result<string>> Run(CodeRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        return ValueTask.FromResult(Execute(run.Code, run.InputJson, cancellationToken));
    }

    /// <summary>Compiles <paramref name="code"/> (once per text) and runs it on <paramref name="inputJson"/>.</summary>
    /// <param name="code">The method body, as a node's 'code' holds it.</param>
    /// <param name="inputJson">The input as JSON text, as the engine passes it.</param>
    /// <param name="cancellationToken">Cancels the compile.</param>
    /// <returns>The result as JSON text, or SND-CODE-001 (does not compile) or SND-CODE-004 (threw, or a result type this port does not write).</returns>
    public static Result<string> Execute(string code, string inputJson, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(inputJson);
        if (!_compiled.TryGetValue(code, out var bytes))
        {
            var compiled = new CodeCompiler().Compile(code, cancellationToken);
            if (compiled.Assembly is null)
            {
                return SindriErrors.CodeCompileFailed.With(string.Join("; ", compiled.Diagnostics.Select(static d => $"{d.Line}:{d.Column} {d.Id} {d.Message}")));
            }

            bytes = _compiled.GetOrAdd(code, compiled.Assembly);
        }

        var context = new AssemblyLoadContext("in-process-code", isCollectible: true);
        try
        {
            using var stream = new MemoryStream(bytes);
            var run = context.LoadFromStream(stream).GetType("SindriCodeBody")?.GetMethod("Run") ?? throw new InvalidOperationException("the compiled body has no SindriCodeBody.Run");
            return run.Invoke(null, [JsonNode.Parse(inputJson)]) switch
            {
                null => "null",
                JsonNode node => node.ToJsonString(),
                string text => JsonValue.Create(text).ToJsonString(),
                var other => SindriErrors.CodeFailed.With($"this test port writes only JsonNode, string or null results, not {other.GetType().Name}"),
            };
        }
        catch (TargetInvocationException e)
        {
            return SindriErrors.CodeFailed.With($"{e.InnerException?.GetType().Name}: {e.InnerException?.Message}");
        }
        finally
        {
            context.Unload();
        }
    }
}
