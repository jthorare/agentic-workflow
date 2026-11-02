using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The collector's transcript lookup follows a separate-login launch into its agent home (sandbox spec SB-8).</summary>
[Trait("Lane", "integration")]
public sealed class TranscriptFilesAgentHomeTests
{
    [Fact]
    public void A_separate_login_launchs_transcript_under_its_agent_home_is_found()
    {
        var owner = Directory.CreateTempSubdirectory("aw-owner-").FullName;
        var agentHome = Directory.CreateTempSubdirectory("aw-home-").FullName;
        try
        {
            var file = Path.Combine(agentHome, "projects", "-repo", "sess-1.jsonl");
            _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "{}");
            var files = new TranscriptFiles(owner, new Dictionary<string, string>(StringComparer.Ordinal) { ["~/.claude"] = agentHome });

            _ = Assert.NotNull(files.LastWrite(Agent("~/.claude/projects/*/{sessionId}.jsonl"), SessionId.From("sess-1"), hinted: null));
        }
        finally
        {
            Directory.Delete(owner, recursive: true);
            Directory.Delete(agentHome, recursive: true);
        }
    }

    [Fact]
    public void An_owner_copy_launchs_transcript_under_the_owner_home_is_still_found()
    {
        var owner = Directory.CreateTempSubdirectory("aw-owner-").FullName;
        var agentHome = Directory.CreateTempSubdirectory("aw-home-").FullName;
        try
        {
            var file = Path.Combine(owner, ".claude", "projects", "-repo", "sess-1.jsonl");
            _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "{}");
            var files = new TranscriptFiles(owner, new Dictionary<string, string>(StringComparer.Ordinal) { ["~/.claude"] = agentHome });

            _ = Assert.NotNull(files.LastWrite(Agent("~/.claude/projects/*/{sessionId}.jsonl"), SessionId.From("sess-1"), hinted: null));
        }
        finally
        {
            Directory.Delete(owner, recursive: true);
            Directory.Delete(agentHome, recursive: true);
        }
    }

    private static AgentEntry Agent(string glob) => new(AgentId.From("claude-code"), "Claude Code", "claude", ProcessName: null, Transcripts: glob, Resume: null);
}
