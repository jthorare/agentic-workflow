#pragma warning disable CA1822, MA0041, MA0182, CS0067 // why: the red stubs read nothing yet

using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One change of the since-you-looked strip.</summary>
internal sealed class LinearSinceRowViewModel
{
    internal LinearSinceRowViewModel(LinearSinceItem item)
    {
        throw new NotSupportedException();
    }

    /// <summary>The issue key.</summary>
    public string Key => throw new NotSupportedException();

    /// <summary>"ENG-12 assigned to you by Sam Lee".</summary>
    public string Text => throw new NotSupportedException();
}
