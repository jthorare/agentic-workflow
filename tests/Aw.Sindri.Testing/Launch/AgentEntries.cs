using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Testing.Launch;

/// <summary>Catalog entries shaped like config/launcher/builtins.json's, so argv tests need no file read, and one added entry.</summary>
public static class AgentEntries
{
    private const string Uuid = "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$";

    /// <summary>The built-in claude-code entry: pre-chosen UUID.</summary>
    public static AgentEntry ClaudeCode { get; } = new(AgentId.From("claude-code"), "Claude Code", "claude", ProcessName: "claude", Transcripts: null, Resume: ["{bin}", "--resume", "{sessionId}"])
    {
        Open = ["{bin}", "--session-id", "{sessionId}", "-n", "{name}", "{seed}"],
        SessionIdMode = SessionIdMode.PreChosenUuid,
        SessionIdPattern = Uuid,
        Usage = "claude-code",
        ModelForm = ModelForm.ClaudeFlag,
        PolicyForm = PolicyForm.ClaudeSettingsFile,
    };

    /// <summary>The built-in codex entry: a reported id.</summary>
    public static AgentEntry Codex { get; } = new(AgentId.From("codex"), "Codex", "codex", ProcessName: "codex", Transcripts: null, Resume: ["{bin}", "resume", "{sessionId}"])
    {
        Open = ["{bin}", "-C", "{cwd}", "{seed}"],
        SessionIdMode = SessionIdMode.Reported,
        SessionIdPattern = Uuid,
        Usage = "codex",
        ModelForm = ModelForm.CodexFlag,
        PolicyForm = PolicyForm.CodexOverrides,
    };

    /// <summary>The built-in cursor entry: a reported id, and an embedded placeholder in its resume template.</summary>
    public static AgentEntry Cursor { get; } = new(AgentId.From("cursor"), "Cursor", "agent", ProcessName: "agent", Transcripts: null, Resume: ["{bin}", "--resume={sessionId}"])
    {
        Open = ["{bin}", "{seed}"],
        SessionIdMode = SessionIdMode.Reported,
        SessionIdPattern = Uuid,
        Usage = "cursor",
    };

    /// <summary>A user-added entry with no id and no resume.</summary>
    public static AgentEntry Aider { get; } = new(AgentId.From("aider"), "Aider", "aider", ProcessName: "aider", Transcripts: null, Resume: null)
    {
        Open = ["{bin}", "--message", "{seed}"],
        Source = CatalogSource.Added,
    };
}
