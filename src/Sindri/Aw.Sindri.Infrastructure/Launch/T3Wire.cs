using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The JSON shapes Sindri reads from and sends to T3 Code: its MCP tools' arguments and structured results, its runtime state file and the
/// 't3 auth session issue --json' output. Only the fields Sindri uses; T3 adds others freely.
/// </summary>
internal static class T3Wire
{
    /// <summary>'userdata/server-runtime.json': where the running server listens.</summary>
    internal sealed record RuntimeState(string Origin);

    /// <summary>'t3 auth session issue --json'.</summary>
    internal sealed record IssuedSession(string SessionId, string Token, DateTimeOffset ExpiresAt);

    /// <summary>An 'OrchestratorMcpFailure' in a tool error's text content.</summary>
    internal sealed record Failure(string Code, string Message);

    /// <summary>A tool that takes no arguments.</summary>
    internal sealed record NoArgs;

    /// <summary>'t3_environment_read' result.</summary>
    internal sealed record Environment(string EnvironmentId, string ServerVersion);

    /// <summary>'orchestrator_capabilities' result.</summary>
    internal sealed record Capabilities(IReadOnlyList<Provider> Providers);

    /// <summary>One provider instance: its driver ('claudeAgent', 'codex', 'cursor', …), its models and why it can't run ('Provider instance is disabled.').</summary>
    internal sealed record Provider(string ProviderInstanceId, string DriverKind, IReadOnlyList<Model> Models, IReadOnlyList<string>? Constraints);

    /// <summary>One model.</summary>
    internal sealed record Model(string Id);

    /// <summary>'t3_thread_launch''s model choice.</summary>
    internal sealed record ModelSelection(string InstanceId, string Model);

    /// <summary>'t3_project_list' arguments.</summary>
    internal sealed record ProjectListArgs(int Cursor, int Limit);

    /// <summary>'t3_project_list' result.</summary>
    internal sealed record ProjectList(IReadOnlyList<Project> Projects, int? NextCursor);

    /// <summary>One project.</summary>
    internal sealed record Project(string Id, string WorkspaceRoot, RepositoryIdentity? RepositoryIdentity);

    /// <summary>The repository T3 knows a project as.</summary>
    internal sealed record RepositoryIdentity(string CanonicalKey);

    /// <summary>'t3_thread_launch' arguments.</summary>
    internal sealed record LaunchArgs(
        string ProjectId,
        string Title,
        string Message,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Workspace? WorkspaceStrategy,   // T3 takes an absent strategy as the project folder, and refuses null
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ModelSelection? ModelSelection);   // absent: the project's default

    /// <summary>The launch's workspace binding ('existing_worktree' only; the project root is the tool's default).</summary>
    internal sealed record Workspace(string Type, string WorktreePath);

    /// <summary>'t3_thread_launch' result.</summary>
    internal sealed record Launched(string ThreadId);

    /// <summary>'t3_thread_read' and 't3_thread_interrupt' arguments.</summary>
    internal sealed record ThreadArgs(string ThreadId);

    /// <summary>'t3_thread_read' result.</summary>
    internal sealed record ThreadRead(Thread Thread);

    /// <summary>A thread's state.</summary>
    internal sealed record Thread(string ThreadId, string Status, bool Archived);
}
