namespace Aw.Context.Core.Engine;

/// <summary>An agent CLI a model call can run on ('context.models' names them claude-code, codex and cursor).</summary>
public enum ModelHarness
{
    /// <summary>The claude CLI ('claude-code'; the model request's key 'claude').</summary>
    ClaudeCode,

    /// <summary>The codex CLI ('codex').</summary>
    Codex,

    /// <summary>The cursor-agent CLI ('cursor').</summary>
    Cursor,
}
