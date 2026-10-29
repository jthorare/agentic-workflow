namespace Aw.Context.Core.Engine;

/// <summary>Where a resolved model name came from (the 'source' of a try in the ledger event).</summary>
public enum ModelSource
{
    /// <summary>The request named it (ModelRequest.Models or .Model).</summary>
    Request,

    /// <summary>'context.models.&lt;kind&gt;.models'.</summary>
    KindModels,

    /// <summary>'context.models.default.models'.</summary>
    DefaultModels,

    /// <summary>'context.models.names' (or the old 'context.conceptScan.models', folded into it).</summary>
    Names,

    /// <summary>The built-in table.</summary>
    Builtin,
}
