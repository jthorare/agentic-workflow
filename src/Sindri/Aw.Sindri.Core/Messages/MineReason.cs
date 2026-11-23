namespace Aw.Sindri.Core.Messages;

/// <summary>Why a thread is mine (messages spec §7.1), in the spec's order.</summary>
public enum MineReason
{
    /// <summary>A direct message or group DM: always mine.</summary>
    DirectMessage,

    /// <summary>I wrote at least one message in the thread.</summary>
    WroteInThread,

    /// <summary>A message in it names me (&lt;@me&gt;), not a broadcast.</summary>
    NamedMe,

    /// <summary>I follow it (Release 2).</summary>
    Followed,
}
