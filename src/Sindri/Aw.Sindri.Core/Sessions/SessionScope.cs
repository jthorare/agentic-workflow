namespace Aw.Sindri.Core.Sessions;

/// <summary>Which sessions discovery lists (launcher.json 'sessions.scope').</summary>
public enum SessionScope
{
    /// <summary>Every agent session the box runs: the real app.</summary>
    All = 0,

    /// <summary>Only sessions on Sindri's own tmux sockets and sessions Sindri launched: a scratch home sees an empty box.</summary>
    Own = 1,
}
