namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>The rule ids a denial carries (sandbox spec §3.6). The renderer and the app key their fix text on these.</summary>
public static class DenialRules
{
    /// <summary>Network: no host group of the plan lists the host, or the port is not 80 or 443.</summary>
    public const string NoGroup = "no-group";

    /// <summary>Network: the host resolves to a loopback, link-local, private or reserved address no group lists.</summary>
    public const string PrivateAddress = "private-address";

    /// <summary>Network: a per-run proxy cap (connections or bytes) was hit.</summary>
    public const string Cap = "cap";

    /// <summary>Policy: the CLI's inner policy refused a tool call.</summary>
    public const string InnerPolicy = "inner-policy";

    /// <summary>Path: a failed tool call named a path on the plan's deny list.</summary>
    public const string DenyList = "deny-list";

    /// <summary>Path: a failed tool call named a path under $HOME that the plan does not bind (a likely denial).</summary>
    public const string NotBound = "not-bound";

    /// <summary>Policy: a hook failed open (the per-launch failopen.log).</summary>
    public const string HookFailOpen = "hook-fail-open";

    /// <summary>The overflow row after 200 distinct targets in a run.</summary>
    public const string Overflow = "overflow";
}
