#pragma warning disable CA1822, MA0041 // a red stub: its members throw until the viewer is written
namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>One finding of a findings fact as the owner reads it (model-check design §4.3).</summary>
internal sealed class FindingRowViewModel : ObservableObject
{
    internal FindingRowViewModel(string rule, string message, string? path, int? line, string? detail)
    {
        _ = (rule, message, path, line, detail);
        throw new NotSupportedException();
    }

    /// <summary>What assistive tech reads for the row: the message, then where it is.</summary>
    public string Name => throw new NotSupportedException();

    /// <summary>The finding's message, the line the owner reads first.</summary>
    public string Message => throw new NotSupportedException();

    /// <summary>The path, line and rule, quiet, after the message; empty when there are none.</summary>
    public string Meta => throw new NotSupportedException();

    /// <summary>True when there is a meta line.</summary>
    public bool HasMeta => throw new NotSupportedException();

    /// <summary>A plain finding's detail, as selectable text; empty for a trace finding or one with no detail.</summary>
    public string Detail => throw new NotSupportedException();

    /// <summary>True when there is a plain detail.</summary>
    public bool HasDetail => throw new NotSupportedException();

    /// <summary>A violation's steps, one per line, in order.</summary>
    public IReadOnlyList<string> Steps => throw new NotSupportedException();

    /// <summary>True when the finding has a step list and a full trace.</summary>
    public bool HasSteps => throw new NotSupportedException();

    /// <summary>The expander button's name: "Full trace, N states".</summary>
    public string TraceName => throw new NotSupportedException();

    /// <summary>Everything after "Full trace:".</summary>
    public string FullTrace => throw new NotSupportedException();

    /// <summary>True while the full trace is shown.</summary>
    public bool IsTraceOpen { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
}
