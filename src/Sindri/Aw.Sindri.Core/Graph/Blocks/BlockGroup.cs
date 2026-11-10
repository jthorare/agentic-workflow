namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The group a block sits in on the workflow picker, in the order the picker shows them.</summary>
public enum BlockGroup
{
    /// <summary>Blocks that start a run.</summary>
    Triggers = 1,

    /// <summary>Blocks that read data in.</summary>
    Data,

    /// <summary>Blocks that ask a model or an agent.</summary>
    AI,

    /// <summary>Blocks that decide, loop or remember.</summary>
    Logic,

    /// <summary>Blocks that send something out.</summary>
    Outputs,
}
