namespace Aw.Context.Core.Models;

/// <summary>How much model a call needs. The port maps a tier to a model per agent CLI; no tier names a model.</summary>
public enum ModelTier
{
    /// <summary>The small fast model each agent CLI already runs by default.</summary>
    Cheap = 1,

    /// <summary>A stronger model, for work too large for the cheap one to hold well.</summary>
    Strong = 2,
}
