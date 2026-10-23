namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>A thread to start.</summary>
/// <param name="ProjectId">The T3 project.</param>
/// <param name="Title">The thread's title.</param>
/// <param name="Message">Its first message.</param>
/// <param name="WorktreePath">An existing worktree to run in, or null for the project folder.</param>
/// <param name="Model">The provider instance and model to run, or null for the project's default.</param>
internal sealed record T3LaunchRequest(string ProjectId, string Title, string Message, string? WorktreePath, T3Model? Model = null);
