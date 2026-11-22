using Aw.Transcripts.Core;
using Aw.Transcripts.Infrastructure;

namespace Aw.Transcripts.Tests.Corpus;

public sealed class DefaultRootsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_default_roots_are_each_providers_home_and_sindris_agent_homes()
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["HOME"] = "/home/dev", ["AW_SINDRI_HOME"] = "/srv/sindri" };

        var roots = SettingsFromEnvironment.Read(k => env.GetValueOrDefault(k)).Roots;

        Assert.Equal(
        [
            new TranscriptRoot(TranscriptProvider.ClaudeCode, "/home/dev/.claude/projects"),
            new TranscriptRoot(TranscriptProvider.Codex, "/home/dev/.codex/sessions"),
            new TranscriptRoot(TranscriptProvider.Cursor, "/home/dev/.cursor/projects"),
            new TranscriptRoot(TranscriptProvider.ClaudeCode, "/srv/sindri/agent-homes/claude-code/projects"),
            new TranscriptRoot(TranscriptProvider.Codex, "/srv/sindri/agent-homes/codex/sessions"),
        ], roots);
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_a_sindri_home_the_agent_homes_sit_under_the_state_dir()
    {
        var roots = SettingsFromEnvironment.Read(k => k == "HOME" ? "/home/dev" : null).Roots;

        Assert.Contains(new TranscriptRoot(TranscriptProvider.Codex, "/home/dev/.agentic-workflow/sindri-app/agent-homes/codex/sessions"), roots);
    }
}
