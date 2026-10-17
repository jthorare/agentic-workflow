using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The terminal viewer the launcher uses: the one launcher.json names when it is valid, else the one in-memory detection chose
/// (once per service start and again on Check again, never per probe; launcher spec §4 "Missing file").
/// </summary>
/// <param name="catalog">The reloading catalog: its current read holds the viewer section and the problems.</param>
/// <param name="create">Builds the viewer adapter for a section.</param>
/// <param name="detection">What the detection reads.</param>
internal sealed class ConfiguredViewer(ReloadingAgentCatalog catalog, Func<ViewerSection, ITerminalViewer> create, DetectionInputs detection) : ITerminalViewer, IDisposable
{
    private readonly SemaphoreSlim _detecting = new(1, 1);
    private (ViewerSection Section, ITerminalViewer Viewer)? _file;
    private (DetectedTerminal Chosen, ITerminalViewer Viewer)? _detected;

    /// <inheritdoc/>
    public string Kind => "configured";

    /// <inheritdoc/>
    public void Dispose() => _detecting.Dispose();

    /// <summary>Forgets the detection, so the next use runs it again (the app's Check again).</summary>
    public void CheckAgain() => _detected = null;

    /// <inheritdoc/>
    public async ValueTask<ViewerOutcome> Open(ViewerRequest request, CancellationToken cancellationToken)
    {
        var (viewer, _) = await Current(cancellationToken).ConfigureAwait(false);
        return await viewer.Open(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<TerminalFacts> Probe(CancellationToken cancellationToken)
    {
        var config = catalog.Current;
        var (viewer, source) = await Current(cancellationToken).ConfigureAwait(false);
        var facts = await viewer.Probe(cancellationToken).ConfigureAwait(false);
        var invalid = config.Unusable || config.AttendedProblems.Count > 0;
        var detail = invalid ? (config.ProblemLine > 0 ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"line {config.ProblemLine}: ") : string.Empty) + (config.Problem ?? (config.AttendedProblems.Count > 0 ? config.AttendedProblems[0] : null)) : facts.Detail;
        return facts with { Source = source, ConfigPath = catalog.Path, Problem = invalid ? "config-invalid" : facts.Problem, Detail = detail };
    }

    private async ValueTask<(ITerminalViewer Viewer, string Source)> Current(CancellationToken cancellationToken)
    {
        var config = catalog.Current;
        if (config.Viewer is { } section && !config.Unusable && config.AttendedProblems.Count == 0)
        {
            if (_file is not { } cached || cached.Section != section)
            {
                _file = (section, create(section));
            }

            return (_file.Value.Viewer, "file");
        }

        await _detecting.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_detected is null)
            {
                var chosen = await TerminalDetection.Detect(detection, cancellationToken).ConfigureAwait(false);
                _detected = (chosen, chosen.Template is { } template
                    ? create(new ViewerSection("exec-template") { Name = template.Name, Argv = template.Argv })
                    : create(new ViewerSection(chosen.Kind)));
            }

            return (_detected.Value.Viewer, "detected");
        }
        finally
        {
            _ = _detecting.Release();
        }
    }
}
