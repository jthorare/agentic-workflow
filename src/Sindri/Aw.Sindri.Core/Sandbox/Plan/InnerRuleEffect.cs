namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>Whether an inner rule allows or denies.</summary>
public enum InnerRuleEffect
{
    /// <summary>Allows the call.</summary>
    Allow,

    /// <summary>Denies the call.</summary>
    Deny,

    /// <summary>Asks the person at the keyboard (an attended session).</summary>
    Ask,
}
