namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The type of one config field.</summary>
public enum BlockFieldType
{
    /// <summary>Text; it may hold references.</summary>
    Text = 1,

    /// <summary>A true or false.</summary>
    Boolean,

    /// <summary>A number.</summary>
    Number,

    /// <summary>A list of text values.</summary>
    TextList,

    /// <summary>A JSON object (its own members are the block's business).</summary>
    Map,
}
