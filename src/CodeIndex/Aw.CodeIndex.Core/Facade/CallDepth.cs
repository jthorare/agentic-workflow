namespace Aw.CodeIndex.Core.Facade;

/// <summary>How many levels a callers or callees walk follows (1 to 8; spec §7 said 1 to 3, raised per owner q93, 2026-10-23).</summary>
public enum CallDepth
{
    /// <summary>Direct callers or callees only.</summary>
    One = 1,

    /// <summary>Two levels.</summary>
    Two = 2,

    /// <summary>Three levels.</summary>
    Three = 3,

    /// <summary>Four levels.</summary>
    Four = 4,

    /// <summary>Five levels.</summary>
    Five = 5,

    /// <summary>Six levels.</summary>
    Six = 6,

    /// <summary>Seven levels.</summary>
    Seven = 7,

    /// <summary>Eight levels.</summary>
    Eight = 8,
}
