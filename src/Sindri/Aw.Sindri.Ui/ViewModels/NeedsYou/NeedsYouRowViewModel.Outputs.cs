using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The files a desk task's Complete carries (<see cref="NeedsYouRowViewModel"/>, continued): one input per required output, shown in the row's sheet.</summary>
internal sealed partial class NeedsYouRowViewModel
{
    /// <summary>The required outputs of the row's desk task, one input each; empty until the shell has read the task, and for a task that needs none.</summary>
    public IReadOnlyList<DeskOutputViewModel> Outputs { get; private set; } = [];

    /// <summary>True when the task has required outputs.</summary>
    public bool HasOutputs => Outputs.Count > 0;

    /// <summary>"Pick &lt;key&gt;" for the first output not given yet; empty when every one is given.</summary>
    public string OutputHint => Outputs.FirstOrDefault(o => !o.IsSet) is { } missing ? Labels.PickOutput(missing.Key) : string.Empty;

    /// <summary>True when Complete may be pressed: every required output has a path or a chosen file.</summary>
    public bool CanComplete => Outputs.All(o => o.IsSet);

    /// <summary>Gives the row the inputs for its task's required outputs.</summary>
    /// <param name="outputs">One input per required output.</param>
    internal void SetOutputs(IReadOnlyList<DeskOutputViewModel> outputs)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        Outputs = outputs;
        foreach (var output in outputs)
        {
            output.PropertyChanged += (_, e) =>
            {
                if (string.Equals(e.PropertyName, nameof(DeskOutputViewModel.IsSet), StringComparison.Ordinal))
                {
                    OnChanged(nameof(CanComplete));
                    OnChanged(nameof(OutputHint));
                }
            };
        }

        OnChanged(nameof(Outputs));
        OnChanged(nameof(HasOutputs));
        OnChanged(nameof(CanComplete));
        OnChanged(nameof(OutputHint));
    }

    /// <summary>Reads the files the inputs stand for.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The contents by output key, or null when one is missing or can't be used (its box says why).</returns>
    internal async Task<Dictionary<string, byte[]>?> ReadOutputs(CancellationToken cancellationToken)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in Outputs)
        {
            if (!output.IsSet || await output.Content(cancellationToken).ConfigureAwait(true) is not { } content)
            {
                return null;
            }

            files[output.Key] = content;
        }

        return files;
    }
}
