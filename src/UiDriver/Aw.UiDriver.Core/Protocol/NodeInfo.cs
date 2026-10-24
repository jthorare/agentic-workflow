namespace Aw.UiDriver.Core.Protocol;

/// <summary>One element of the tree as the driver reports it. Bounds are in the window's coordinates, in device-independent pixels.</summary>
/// <param name="Id">The node id: child positions from the window ('0/2/1').</param>
/// <param name="Type">The type name.</param>
/// <param name="AutomationId">The automation id, or null.</param>
/// <param name="Name">The automation name or x:Name, or null.</param>
/// <param name="Text">The shown text, or null.</param>
/// <param name="Visible">Whether it is visible.</param>
/// <param name="Enabled">Whether it takes input.</param>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
/// <param name="Children">Its children (empty in a search answer and below the depth a tree was asked for).</param>
internal sealed record NodeInfo(
    string Id,
    string Type,
    string? AutomationId,
    string? Name,
    string? Text,
    bool Visible,
    bool Enabled,
    double X,
    double Y,
    double Width,
    double Height,
    IReadOnlyList<NodeInfo> Children);
