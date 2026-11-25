namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>The six state classes of a row, as the API serves them. Together with the row's kind they pick the drawn glyph.</summary>
internal enum RowStateClass
{
    /// <summary>Not started.</summary>
    Open,

    /// <summary>Under way.</summary>
    Active,

    /// <summary>Waiting on someone or something.</summary>
    Waiting,

    /// <summary>Blocked.</summary>
    Blocked,

    /// <summary>Finished.</summary>
    Done,

    /// <summary>Failed.</summary>
    Failed,
}
