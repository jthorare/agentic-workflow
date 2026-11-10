using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'code' block: a C# method body, compiled with Roslyn and run in the sandboxed 'sindri-code-runner' process (agent-first PR 9c). It hands on the returned value as 'result'.</summary>
/// <param name="runner">Compiles and runs the body.</param>
public sealed class CodeBlock(ICodeRunPort runner) : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["result"] = "any?" });

    /// <inheritdoc />
    public string Type => "code";

    /// <inheritdoc />
    public string Title => "Code";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Logic;

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("input", BlockFieldType.Text, Required: false),
        new("code", BlockFieldType.Text),
        new("repo", BlockFieldType.Text, Required: false),
        new("branch", BlockFieldType.Text, Required: false),
        new("timeout", BlockFieldType.Text, Required: false),
        new("memoryMb", BlockFieldType.Number, Required: false),
    ]);

    /// <inheritdoc />
    public string Example => """{"input":"{{triage.items}}","code":"return input!.AsArray().Where(i => i![\"score\"]!.GetValue<int>() > 100).Select(i => i![\"title\"]!.GetValue<string>()).ToArray();"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken) => throw new NotSupportedException($"{runner is null}");
}
