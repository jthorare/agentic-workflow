using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One change of the since-you-looked strip.</summary>
/// <param name="item">The change.</param>
internal sealed class LinearSinceRowViewModel(LinearSinceItem item)
{
    /// <summary>The issue key.</summary>
    public string Key { get; } = item.Key;

    /// <summary>"ENG-12 assigned to you by Sam Lee".</summary>
    public string Text { get; } = Labels.LinearSinceRow(item);
}
