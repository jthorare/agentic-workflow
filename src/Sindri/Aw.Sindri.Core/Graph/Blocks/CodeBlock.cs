using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'code' block: a C# method body, compiled with Roslyn and run in the sandboxed 'sindri-code-runner' process (agent-first PR 9c). It hands on the returned value as 'result'.</summary>
/// <param name="runner">Compiles and runs the body.</param>
public sealed class CodeBlock(ICodeRunPort runner) : IGraphBlock
{
    private const int DefaultMemoryMb = 256;
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _longestTimeout = TimeSpan.FromSeconds(60);
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
        new("input", BlockFieldType.Text, Required: false, Caption: "Input"),
        new("code", BlockFieldType.Text, Caption: "Code"),
        new("repo", BlockFieldType.Text, Required: false, Caption: "Repository"),
        new("branch", BlockFieldType.Text, Required: false, Caption: "Branch"),
        new("timeout", BlockFieldType.Text, Required: false, Caption: "Stop after"),
        new("memoryMb", BlockFieldType.Number, Required: false, Caption: "Memory limit (MB)"),
    ]);

    /// <inheritdoc />
    public string Example => """{"input":"{{triage.items}}","code":"return input!.AsArray().Where(i => i![\"score\"]!.GetValue<int>() > 100).Select(i => i![\"title\"]!.GetValue<string>()).ToArray();"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var run = Build(context.Config, context.RawConfig);
        if (run.IsError)
        {
            return run.Error;
        }

        var answer = await runner.Run(run.Value, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error;
        }

        try
        {
            return new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["result"] = JsonNode.Parse(answer.Value) });
        }
        catch (JsonException e)   // boundary: the runner printed something that is not JSON
        {
            return SindriErrors.CodeFailed.With($"the result is not JSON: {e.Message}");
        }
    }

    // why: the C# is not a template: it is read as written, so a "{{" in it stays
    private static Result<CodeRun> Build(JsonObject config, JsonObject raw)
    {
        var code = ConfigValues.Text(raw["code"]);
        if (string.IsNullOrWhiteSpace(code))
        {
            return SindriErrors.GraphBlockFailed.With("'code' must hold a C# method body (fix: write the body, such as return input;)");
        }

        var timeout = _defaultTimeout;
        if (ConfigValues.Text(config["timeout"]) is { } text)
        {
            var parsed = BlockDuration.Parse(text);
            if (parsed.IsError || parsed.Value > _longestTimeout)
            {
                return SindriErrors.GraphBlockFailed.With($"'timeout' must be a length of time such as 5s, and at most 60 s; '{text}' is not (fix: use 60s or less)");
            }

            timeout = parsed.Value;
        }

        var memory = ConfigValues.Int(config["memoryMb"]) ?? DefaultMemoryMb;
        if (memory is < 16 or > 4096)
        {
            return SindriErrors.GraphBlockFailed.With("'memoryMb' must be between 16 and 4096");
        }

        var input = config["input"]?.ToJsonString() ?? "null";
        return new CodeRun(code, input, ConfigValues.Text(config["repo"]), ConfigValues.Text(config["branch"]), timeout, memory);
    }
}
