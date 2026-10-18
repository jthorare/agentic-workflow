using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launch.Argv;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch.Argv;

/// <summary>BuildArgv (launcher spec section 3): the entry's templates, expanded; pure.</summary>
public sealed class ArgvBuilderTests
{
    private const string Uuid = "0b1f6c3e-52d4-4c7a-9a58-3d1e7f9a2b10";

    private static ArgvInputs Inputs(string? sessionId = null, string? name = null) =>
        new(A.FirstItemId, "/work/repo", name ?? $"sindri {A.FirstItemId.Value}", ArgvBuilder.Seed(A.FirstItemId), sessionId is null ? null : SessionId.From(sessionId));

    public static TheoryData<string, string> Goldens => new()
    {
        { "claude-code", "open" }, { "claude-code", "resume" },
        { "codex", "open" }, { "codex", "resume" },
        { "cursor", "open" }, { "cursor", "resume" },
        { "aider-added", "open" },
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Goldens))]
    public void Argv_equals_its_golden(string agent, string mode)
    {
        var entry = agent switch
        {
            "claude-code" => AgentEntries.ClaudeCode,
            "codex" => AgentEntries.Codex,
            "cursor" => AgentEntries.Cursor,
            _ => AgentEntries.Aider,
        };
        var launchMode = mode == "resume" ? LaunchMode.Resume : LaunchMode.Open;

        var argv = ArgvBuilder.Build(entry, launchMode, Inputs(Uuid));

        var text = Encoding.UTF8.GetString(AwJson.Serialize(new ArgvGolden(agent, mode, argv.Value), AwJson.CreateOptions(ArgvGoldenJsonContext.Default, indented: true))) + "\n";
        Golden.Match(text, typeof(ArgvBuilderTests).Assembly, $"Launch/Golden/argv/{agent}-{mode}.json", Normalization.None);
    }

    [Fact, Trait("Lane", "unit")]
    public void Seed_is_the_fixed_template_plus_ids()
    {
        Assert.Equal(
            "Sindri item ITM-00000000000000000000000000000001. Read it with the sindri_item tool before you start; you are taking it over from Sindri.",
            ArgvBuilder.Seed(A.FirstItemId));
    }

    [Fact, Trait("Lane", "unit")]
    public void Resume_on_an_entry_without_resume_is_SND_LAUNCH_006()
    {
        var argv = ArgvBuilder.Build(AgentEntries.Aider, LaunchMode.Resume, Inputs(Uuid));

        Assert.Equal("SND-LAUNCH-006", argv.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Entry_pattern_mismatch_drops_resume_to_open()
    {
        var resumed = ArgvBuilder.Build(AgentEntries.ClaudeCode, LaunchMode.Resume, Inputs("not-a-uuid"));
        var noId = ArgvBuilder.Build(AgentEntries.Codex, LaunchMode.Resume, Inputs());

        Assert.Equal(["codex", "-C", "/work/repo", ArgvBuilder.Seed(A.FirstItemId)], noId.Value);
        // claude's open template needs a usable {sessionId} of its own, and the stored one is not: the open is refused, not guessed
        Assert.Equal("SND-REQ-001", resumed.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pre_chosen_open_with_a_bad_session_id_is_refused()
    {
        var argv = ArgvBuilder.Build(AgentEntries.ClaudeCode, LaunchMode.Open, Inputs("not-a-uuid"));

        Assert.Equal("SND-REQ-001", argv.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Attach_starts_no_process_so_it_has_no_argv()
    {
        var argv = ArgvBuilder.Build(AgentEntries.ClaudeCode, LaunchMode.Attach, Inputs(Uuid));

        Assert.Equal("SND-REQ-001", argv.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_relative_repo_path_is_refused_for_every_entry()
    {
        var inputs = Inputs(Uuid) with { RepoPath = "repo" };

        Assert.Equal("SND-REQ-001", ArgvBuilder.Build(AgentEntries.ClaudeCode, LaunchMode.Open, inputs).Error.Code);
    }
}
