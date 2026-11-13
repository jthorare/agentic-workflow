namespace Aw.Sindri.Features.SetupWatch;

/// <summary>Who made the hello-sindri in the store.</summary>
internal enum HelloOwner
{
    /// <summary>The setup service made it.</summary>
    Ours = 1,

    /// <summary>It is not there, or someone else made it.</summary>
    NotOurs,

    /// <summary>The store could not answer; try again on the next tick.</summary>
    Unknown,
}
