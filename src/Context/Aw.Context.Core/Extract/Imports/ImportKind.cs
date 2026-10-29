namespace Aw.Context.Core.Extract.Imports;

/// <summary>How an import names what it brings in.</summary>
internal enum ImportKind
{
    /// <summary>A named import: 'import { a as b }'.</summary>
    Named,

    /// <summary>A default import: 'import d from'.</summary>
    Default,

    /// <summary>A namespace import: 'import * as ns from'.</summary>
    Namespace,
}
