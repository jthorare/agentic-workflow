namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>What a sandboxed session does with one owner hook (owner, 2026-10-18: hooks help performance and quality, so keep is the default).</summary>
public enum HookTreatment
{
    /// <summary>The hook runs inside the sandbox as it does outside.</summary>
    Keep,

    /// <summary>The session's settings leave the hook out: it answers 'allow' in a way that bypasses 'dontAsk'.</summary>
    Drop,

    /// <summary>The hook runs, and the deny rules also list every denied command in the form the hook rewrites it to.</summary>
    KeepDenyRewrites,
}
