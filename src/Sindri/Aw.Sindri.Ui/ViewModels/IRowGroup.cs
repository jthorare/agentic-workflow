using System.Collections;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>A group of rows under one header: what a sticky group list draws, without knowing what the rows are.</summary>
internal interface IRowGroup
{
    /// <summary>The group's rows; a collection that raises changes is followed live.</summary>
    public IEnumerable Items { get; }
}
