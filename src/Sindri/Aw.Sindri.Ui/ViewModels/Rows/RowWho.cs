namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>Who a row waits on.</summary>
internal enum RowWho
{
    /// <summary>The person using the app.</summary>
    Me,

    /// <summary>Another person.</summary>
    Person,

    /// <summary>An agent.</summary>
    Agent,

    /// <summary>A CI run.</summary>
    Ci,

    /// <summary>A reviewer.</summary>
    Reviewer,

    /// <summary>Nobody: the cell is blank.</summary>
    Nobody,
}
