namespace Aw.Sindri.Host.Models;

/// <summary>Why a harness didn't answer a call, for the pause line.</summary>
internal enum HarnessCause
{
    /// <summary>It is at its usage limit.</summary>
    Limit,

    /// <summary>It failed some other way.</summary>
    Failed,

    /// <summary>It isn't on PATH.</summary>
    NotInstalled,
}
