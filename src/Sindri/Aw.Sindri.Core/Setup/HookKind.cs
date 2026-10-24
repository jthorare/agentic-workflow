namespace Aw.Sindri.Core.Setup;

/// <summary>What a pre-commit file is, to the installer and the doctor.</summary>
public enum HookKind
{
    /// <summary>No file.</summary>
    None,

    /// <summary>Sindri's current hook (marker '# sindri-pre-commit v3'): left alone, or rewritten with a new binary path.</summary>
    V3,

    /// <summary>The TS build's hook (marker '# sindri-pre-commit v2'): replaced.</summary>
    V2,

    /// <summary>The TS build's first hook (marker '# sindri-scrub-pre-commit v1'): replaced.</summary>
    V1,

    /// <summary>Anyone else's hook, or a file that only mentions a marker: never overwritten (SND-SCRUB-003).</summary>
    Foreign,
}
