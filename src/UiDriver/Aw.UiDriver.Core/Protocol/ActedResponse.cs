using System.Text.Json.Serialization;

namespace Aw.UiDriver.Core.Protocol;

/// <summary>A verb that acted on an element and has nothing more to say.</summary>
/// <param name="Node">The element it acted on.</param>
/// <param name="Pointer">
/// Hover only (#3488): "real" when the driver's pointer landed on the element (IsPointerOver holds), "class" when it never did and only the
/// :pointerover class is set. Null, and left out of the JSON, for every other verb and for `hover --off`.
/// </param>
internal sealed record ActedResponse(NodeInfo Node, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Pointer = null)
{
    /// <summary>The pointer landed on the element.</summary>
    internal const string RealPointer = "real";

    /// <summary>The pointer never landed; only the :pointerover class is set.</summary>
    internal const string ClassOnly = "class";
}
