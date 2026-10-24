
namespace Aw.UiDriver.Core.Selectors;

/// <summary>What a wait waits for.</summary>
internal enum WaitState
{
    /// <summary>The element is in the tree.</summary>
    Exists,

    /// <summary>The element is in the tree and visible.</summary>
    Visible,

    /// <summary>The element is in the tree and not visible.</summary>
    Hidden,

    /// <summary>The element is not in the tree.</summary>
    Gone,

    /// <summary>The element is in the tree and takes input.</summary>
    Enabled,

    /// <summary>The element is in the tree and does not take input.</summary>
    Disabled,

    /// <summary>The element shows exactly the text.</summary>
    TextEquals,

    /// <summary>The element shows text that contains the text.</summary>
    TextContains,
}
