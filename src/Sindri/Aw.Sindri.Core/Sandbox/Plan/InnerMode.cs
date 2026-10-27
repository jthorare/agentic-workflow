namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>What the agent CLI's own policy does with a call outside its rules.</summary>
public enum InnerMode
{
    /// <summary>Refuses it (a Step: nobody is there to ask).</summary>
    Refuse,

    /// <summary>Asks the person at the keyboard (an attended session).</summary>
    Ask,
}
