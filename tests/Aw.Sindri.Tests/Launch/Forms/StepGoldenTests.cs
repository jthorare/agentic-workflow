using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launch.Argv;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch.Forms;

/// <summary>The unattended-open argv of each built-in, pinned as goldens (AW_GOLDEN_UPDATE=1 rewrites them).</summary>
public sealed class StepGoldenTests
{
    private static readonly StepRef _ref = new("build", "agent");

    private static readonly StepPolicy _policy = new(["Read", "Grep"], StepPermissionMode.ReadOnly, "/work/repo/.claude/worktrees/sindri-1");

    public static TheoryData<string, string> Cases => new()
    {
        { "claude-code", "step-open" },
        { "codex", "step-open" },
        { "cursor", "step-open-013" },
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Cases))]
    public void Step_open_argv_equals_its_golden(string agent, string mode)
    {
        var entry = agent switch
        {
            "claude-code" => AgentEntries.ClaudeCode,
            "codex" => AgentEntries.Codex,
            _ => AgentEntries.Cursor,
        };
        var inputs = new ArgvInputs(
            A.FirstItemId, "/work/repo", "sindri step", ArgvBuilder.Seed(A.FirstItemId), SessionId.From("0b1f6c3e-52d4-4c7a-9a58-3d1e7f9a2b10"),
            Step: new StepArgs("the-model", _policy, _ref), PolicyFilePath: "/state/launch/policy/LCH-1.settings.json");

        var argv = ArgvBuilder.Build(entry, LaunchMode.Open, inputs);

        var golden = argv.IsError ? new ArgvStepGolden(agent, mode, Argv: null, argv.Error.Code) : new ArgvStepGolden(agent, mode, argv.Value, Error: null);
        var text = Encoding.UTF8.GetString(AwJson.Serialize(golden, AwJson.CreateOptions(ArgvStepGoldenJsonContext.Default, indented: true))) + "\n";
        Golden.Match(text, typeof(StepGoldenTests).Assembly, $"Launch/Golden/argv/{agent}-{mode}.json", Normalization.None);
    }
}
