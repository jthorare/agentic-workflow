namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>How one fact cell of a row is drawn.</summary>
internal enum RowFactKind
{
    /// <summary>Plain text.</summary>
    Text,

    /// <summary>A status chip.</summary>
    Chip,

    /// <summary>A 56 by 4 bar and its count.</summary>
    Progress,

    /// <summary>Added and removed line counts.</summary>
    Diffstat,

    /// <summary>A reference that shortens in the middle.</summary>
    Ref,

    /// <summary>A 16 px avatar and a name.</summary>
    Person,

    /// <summary>A number.</summary>
    Count,
}
